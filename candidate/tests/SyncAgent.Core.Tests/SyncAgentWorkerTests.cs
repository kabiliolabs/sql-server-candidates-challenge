using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;
using Xunit;

namespace SyncAgent.Core.Tests
{
    /// <summary>
    /// Exercises the loop end to end with every dependency substituted.
    /// </summary>
    /// <remarks>
    /// The behaviour under test is the one the contract makes load-bearing: the platform's
    /// dequeue is destructive, so every task the agent claims must produce a submitted result
    /// no matter how it goes wrong.
    /// </remarks>
    public sealed class SyncAgentWorkerTests
    {
        private readonly ISyncPlatformClient _platform = Substitute.For<ISyncPlatformClient>();
        private readonly ISyncTaskDispatcher _dispatcher = Substitute.For<ISyncTaskDispatcher>();
        private readonly FakeDelayProvider _delays = new FakeDelayProvider();
        private readonly FakeClock _clock = new FakeClock(TestData.Now);
        private readonly List<SyncTaskResult> _submitted = new List<SyncTaskResult>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private SyncAgentWorker CreateWorker()
        {
            var options = TestData.Options();

            _platform
                .When(client => client.SubmitResultAsync(Arg.Any<SyncTaskResult>(), Arg.Any<CancellationToken>()))
                .Do(call =>
                {
                    _submitted.Add(call.Arg<SyncTaskResult>());

                    // One task per test: stop the loop as soon as its outcome is reported.
                    _cts.Cancel();
                });

            return new SyncAgentWorker(
                _platform,
                _dispatcher,
                new SyncTaskValidator(options, _clock),
                new PassThroughRetryExecutor(),
                _delays,
                _clock,
                options,
                NullLogger<SyncAgentWorker>.Instance);
        }

        private void QueueOnce(SyncTaskRequest task)
        {
            var served = false;

            _platform.GetNextTaskAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (served)
                {
                    return Task.FromResult<SyncTaskRequest?>(null);
                }

                served = true;
                return Task.FromResult<SyncTaskRequest?>(task);
            });
        }

        [Fact]
        public async Task A_task_that_succeeds_is_reported_as_completed()
        {
            var task = TestData.Task();
            QueueOnce(task);
            _dispatcher
                .DispatchAsync(task, Arg.Any<CancellationToken>())
                .Returns(new SyncPayload(new object[] { 1, 2, 3 }, wasTruncated: false));

            await CreateWorker().RunAsync(_cts.Token);

            _submitted.Should().ContainSingle();
            _submitted[0].Status.Should().Be(SyncTaskStatus.Completed);
            _submitted[0].RecordCount.Should().Be(3);
            _submitted[0].TaskId.Should().Be(task.TaskId);
            _submitted[0].ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task A_task_with_an_unsupported_type_is_reported_as_failed_rather_than_dropped()
        {
            var task = TestData.Task("GetSomethingElse");
            QueueOnce(task);
            _dispatcher
                .DispatchAsync(task, Arg.Any<CancellationToken>())
                .Returns<SyncPayload>(_ => throw new UnknownTaskTypeException());

            await CreateWorker().RunAsync(_cts.Token);

            _submitted.Should().ContainSingle();
            _submitted[0].Status.Should().Be(SyncTaskStatus.Failed);
            _submitted[0].Data.Should().BeNull();
            _submitted[0].RecordCount.Should().Be(0);
            _submitted[0].ErrorMessage.Should().Be(UnknownTaskTypeException.SanitizedMessage);
        }

        [Fact]
        public async Task A_task_that_fails_validation_never_reaches_the_dispatcher()
        {
            QueueOnce(TestData.Task(taskId: "not-a-ulid"));

            await CreateWorker().RunAsync(_cts.Token);

            await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<SyncTaskRequest>(), Arg.Any<CancellationToken>());
            _submitted.Should().ContainSingle();
            _submitted[0].Status.Should().Be(SyncTaskStatus.Failed);
        }

        [Fact]
        public async Task An_unexpected_failure_is_reported_without_leaking_the_exception_message()
        {
            // A SqlException message can name the server, the database and part of the
            // statement. The platform gets the type name and the task id, which is enough to
            // triage, and the detail stays in the local log.
            const string Sensitive = "Login failed for user 'sa' on server PROD-SQL-01.";
            var task = TestData.Task();
            QueueOnce(task);
            _dispatcher
                .DispatchAsync(task, Arg.Any<CancellationToken>())
                .Returns<SyncPayload>(_ => throw new InvalidOperationException(Sensitive));

            await CreateWorker().RunAsync(_cts.Token);

            _submitted.Should().ContainSingle();
            _submitted[0].Status.Should().Be(SyncTaskStatus.Failed);
            _submitted[0].ErrorMessage.Should().NotContain(Sensitive);
            _submitted[0].ErrorMessage.Should().Contain(nameof(InvalidOperationException));
            _submitted[0].ErrorMessage.Should().Contain(task.TaskId);
        }

        [Fact]
        public async Task An_empty_queue_backs_off_and_does_not_spin()
        {
            _platform.GetNextTaskAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<SyncTaskRequest?>(null));

            var options = TestData.Options();
            var delays = new FakeDelayProvider();
            var worker = new SyncAgentWorker(
                _platform,
                _dispatcher,
                new SyncTaskValidator(options, _clock),
                new PassThroughRetryExecutor(),
                delays,
                _clock,
                options,
                NullLogger<SyncAgentWorker>.Instance);

            using (var cts = new CancellationTokenSource())
            {
                // Let it idle a few cycles, then stop.
                _platform.GetNextTaskAsync(Arg.Any<CancellationToken>()).Returns(_ =>
                {
                    if (delays.Delays.Count >= 4)
                    {
                        cts.Cancel();
                    }

                    return Task.FromResult<SyncTaskRequest?>(null);
                });

                await worker.RunAsync(cts.Token);
            }

            delays.Delays.Should().HaveCountGreaterOrEqualTo(4);
            delays.Delays[0].Should().Be(options.PollInterval);
            delays.Delays[1].Should().Be(TimeSpan.FromSeconds(10));
            delays.Delays[2].Should().Be(TimeSpan.FromSeconds(20));
            delays.Delays.Should().OnlyContain(delay => delay <= options.MaxIdleBackoff);
        }

        [Fact]
        public async Task A_failure_to_report_a_result_does_not_kill_the_loop()
        {
            // The task is lost either way — the platform cannot redeliver — but an agent that
            // exits on a transient platform outage needs a human to notice and restart it.
            var task = TestData.Task();
            QueueOnce(task);
            _dispatcher.DispatchAsync(task, Arg.Any<CancellationToken>()).Returns(SyncPayload.Empty);

            _platform
                .When(client => client.SubmitResultAsync(Arg.Any<SyncTaskResult>(), Arg.Any<CancellationToken>()))
                .Do(_ =>
                {
                    _cts.Cancel();
                    throw new InvalidOperationException("platform unreachable");
                });

            var options = TestData.Options();
            var worker = new SyncAgentWorker(
                _platform,
                _dispatcher,
                new SyncTaskValidator(options, _clock),
                new PassThroughRetryExecutor(),
                _delays,
                _clock,
                options,
                NullLogger<SyncAgentWorker>.Instance);

            Func<Task> act = () => worker.RunAsync(_cts.Token);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task A_task_interrupted_by_shutdown_is_still_reported()
        {
            // The single most important shutdown behaviour, and the reason SubmitResultAsync
            // switches to a fresh token bounded by the grace period. The platform's dequeue is
            // destructive, so a task the agent is holding when it is stopped would otherwise
            // vanish with no record on either side. Submitting on the already-cancelled token
            // would cancel the report as well.
            var task = TestData.Task();
            var options = TestData.Options();
            var submitted = new List<SyncTaskResult>();

            using (var cts = new CancellationTokenSource())
            {
                _platform.GetNextTaskAsync(Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<SyncTaskRequest?>(task));

                _dispatcher.DispatchAsync(task, Arg.Any<CancellationToken>()).Returns<SyncPayload>(_ =>
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                });

                _platform
                    .When(client => client.SubmitResultAsync(
                        Arg.Any<SyncTaskResult>(),
                        Arg.Any<CancellationToken>()))
                    .Do(call => submitted.Add(call.Arg<SyncTaskResult>()));

                var worker = new SyncAgentWorker(
                    _platform,
                    _dispatcher,
                    new SyncTaskValidator(options, _clock),
                    new PassThroughRetryExecutor(),
                    _delays,
                    _clock,
                    options,
                    NullLogger<SyncAgentWorker>.Instance);

                await worker.RunAsync(cts.Token);
            }

            submitted.Should().ContainSingle();
            submitted[0].Status.Should().Be(SyncTaskStatus.Failed);
            submitted[0].TaskId.Should().Be(task.TaskId);
            submitted[0].ErrorMessage.Should().Contain("shutting down");
        }

        [Fact]
        public async Task The_executed_at_stamp_comes_from_the_clock()
        {
            var task = TestData.Task();
            QueueOnce(task);
            _dispatcher.DispatchAsync(task, Arg.Any<CancellationToken>()).Returns(SyncPayload.Empty);
            _clock.UtcNow = new DateTimeOffset(2026, 3, 12, 11, 0, 0, TimeSpan.Zero);

            await CreateWorker().RunAsync(_cts.Token);

            _submitted[0].ExecutedAt.Should().Be(_clock.UtcNow);
        }
    }
}
