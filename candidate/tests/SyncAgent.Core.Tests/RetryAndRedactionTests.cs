using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Resilience;
using SyncAgent.Infrastructure.Configuration;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Infrastructure.Resilience;
using Xunit;

namespace SyncAgent.Core.Tests
{
    public sealed class ExponentialBackoffRetryExecutorTests
    {
        private readonly FakeDelayProvider _delays = new FakeDelayProvider();

        private ExponentialBackoffRetryExecutor CreateExecutor(ITransientErrorDetector detector)
        {
            return new ExponentialBackoffRetryExecutor(
                TestData.Options(),
                detector,
                _delays,
                NullLogger<ExponentialBackoffRetryExecutor>.Instance,

                // Pin the jitter at its maximum so the backoff ceiling itself is observable.
                // With a live draw the delays would be uniform over [0, ceiling] and only
                // assertable statistically.
                jitter: () => 1.0);
        }

        [Fact]
        public async Task An_operation_that_succeeds_first_time_is_not_retried()
        {
            var executor = CreateExecutor(new AlwaysTransient());
            var calls = 0;

            await executor.ExecuteAsync("op", _ => { calls++; return Task.FromResult(1); }, CancellationToken.None);

            calls.Should().Be(1);
            _delays.Delays.Should().BeEmpty();
        }

        [Fact]
        public async Task A_transient_failure_is_retried_up_to_the_configured_attempt_count()
        {
            var executor = CreateExecutor(new AlwaysTransient());
            var calls = 0;

            Func<Task> act = () => executor.ExecuteAsync<int>(
                "op",
                _ => { calls++; throw new InvalidOperationException(); },
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();

            // MaxRetryAttempts is the total number of attempts, not the retries after the
            // first one.
            calls.Should().Be(TestData.Options().MaxRetryAttempts);
            _delays.Delays.Should().HaveCount(TestData.Options().MaxRetryAttempts - 1);
        }

        [Fact]
        public async Task The_backoff_doubles_between_attempts()
        {
            var executor = CreateExecutor(new AlwaysTransient());

            try
            {
                await executor.ExecuteAsync<int>("op", _ => throw new InvalidOperationException(), CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Expected: the point of the test is the delays it took getting here.
            }

            _delays.Delays.Should().Equal(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4));
        }

        [Fact]
        public async Task A_non_transient_failure_is_not_retried()
        {
            // Retrying a 400 or a syntax error only delays the failure while adding load.
            var executor = CreateExecutor(new NeverTransient());
            var calls = 0;

            try
            {
                await executor.ExecuteAsync<int>(
                    "op",
                    _ => { calls++; throw new InvalidOperationException(); },
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Expected.
            }

            calls.Should().Be(1);
            _delays.Delays.Should().BeEmpty();
        }

        [Fact]
        public async Task Cancellation_is_not_treated_as_a_transient_failure()
        {
            // Otherwise a stop request would be retried and the process would outlive the
            // window the service control manager allows.
            var executor = CreateExecutor(new AlwaysTransient());

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => executor.ExecuteAsync<int>("op", _ => Task.FromResult(1), cts.Token));
            }
        }

        private sealed class AlwaysTransient : ITransientErrorDetector
        {
            public bool IsTransient(Exception exception) => true;
        }

        private sealed class NeverTransient : ITransientErrorDetector
        {
            public bool IsTransient(Exception exception) => false;
        }
    }

    public sealed class TransientErrorDetectorTests
    {
        private readonly ITransientErrorDetector _detector = new CompositeTransientErrorDetector(
            new ITransientErrorDetector[]
            {
                new SqlTransientErrorDetector(),
                new PlatformTransientErrorDetector(),
            });

        [Fact]
        public void A_transient_platform_failure_is_recognised()
        {
            _detector.IsTransient(
                    new PlatformApiException("boom", HttpStatusCode.ServiceUnavailable, isTransient: true))
                .Should().BeTrue();
        }

        [Fact]
        public void A_permanent_platform_failure_is_not()
        {
            _detector.IsTransient(
                    new PlatformApiException("nope", HttpStatusCode.Unauthorized, isTransient: false))
                .Should().BeFalse();
        }

        [Fact]
        public void A_connection_level_failure_is_transient()
        {
            _detector.IsTransient(new HttpRequestException("connection refused")).Should().BeTrue();
            _detector.IsTransient(new IOException("reset")).Should().BeTrue();
        }

        [Fact]
        public void A_programming_error_is_not_transient()
        {
            _detector.IsTransient(new NullReferenceException()).Should().BeFalse();
            _detector.IsTransient(new ArgumentException()).Should().BeFalse();
        }

        [Fact]
        public void A_wrapped_transient_failure_is_still_recognised()
        {
            // The interesting exception is routinely nested inside a wrapper.
            var wrapped = new InvalidOperationException(
                "outer",
                new PlatformApiException("inner", HttpStatusCode.BadGateway, isTransient: true));

            _detector.IsTransient(wrapped).Should().BeTrue();
        }

        [Fact]
        public void The_inner_exception_walk_stops_at_the_depth_limit()
        {
            // The walk is bounded because an exception chain is not guaranteed acyclic once
            // custom types are involved, and an unbounded walk of one is an infinite loop
            // inside an error handler. The cost is this: a transient cause buried deeper than
            // the limit is not found, and the operation is treated as permanently failed.
            // That is the safe direction to be wrong in — it gives up rather than looping.
            Exception chain = new PlatformApiException("deep", HttpStatusCode.BadGateway, isTransient: true);

            for (var depth = 0; depth < 20; depth++)
            {
                chain = new InvalidOperationException("wrapper", chain);
            }

            _detector.IsTransient(chain).Should().BeFalse();
        }

        [Fact]
        public void A_transient_cause_within_the_depth_limit_is_found()
        {
            Exception chain = new PlatformApiException("cause", HttpStatusCode.BadGateway, isTransient: true);

            for (var depth = 0; depth < 5; depth++)
            {
                chain = new InvalidOperationException("wrapper", chain);
            }

            _detector.IsTransient(chain).Should().BeTrue();
        }
    }

    public sealed class SecretRedactorTests
    {
        [Fact]
        public void An_api_key_is_reduced_to_a_prefix_and_a_length()
        {
            var redacted = SecretRedactor.Redact("candidate-test-key-2026");

            redacted.Should().Be("cand... (23 chars)");
            redacted.Should().NotContain("test-key-2026");
        }

        [Fact]
        public void The_length_is_shown_because_it_is_often_the_diagnosis()
        {
            // A key with a trailing newline from a copy-paste is otherwise invisible.
            SecretRedactor.Redact("abcd\n").Should().Contain("5 chars");
        }

        [Fact]
        public void A_missing_secret_says_so_rather_than_rendering_as_empty()
        {
            SecretRedactor.Redact(null).Should().Be("(not set)");
            SecretRedactor.Redact(string.Empty).Should().Be("(not set)");
        }

        [Fact]
        public void A_short_secret_reveals_no_prefix_at_all()
        {
            SecretRedactor.Redact("ab").Should().Be("... (2 chars)");
        }

        [Theory]
        [InlineData("Server=.;Database=AW;User ID=agent;Password=hunter2")]
        [InlineData("Server=.;Database=AW;User ID=agent;Pwd=hunter2")]
        public void A_connection_string_password_never_survives_redaction(string connectionString)
        {
            // Rebuilt from an allow-list of keywords rather than pattern-stripped: a regex
            // over "Password=" misses the Pwd alias and fails open.
            var redacted = SecretRedactor.RedactConnectionString(connectionString);

            redacted.Should().NotContain("hunter2");
            redacted.Should().Contain("agent");
            redacted.Should().Contain("AW");
        }

        [Fact]
        public void An_unparseable_connection_string_yields_nothing()
        {
            // Nothing can be safely extracted from a string the parser rejects, so the
            // redactor says so rather than falling back to printing it.
            SecretRedactor.RedactConnectionString("this is not a connection string")
                .Should().Be("(unparseable)");
        }
    }
}
