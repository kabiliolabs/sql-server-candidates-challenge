using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Tests
{
    /// <summary>A clock the test moves by hand.</summary>
    internal sealed class FakeClock : ISystemClock
    {
        public FakeClock(DateTimeOffset now)
        {
            UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; set; }

        public void Advance(TimeSpan by) => UtcNow += by;
    }

    /// <summary>
    /// Records what was waited for and returns immediately.
    /// </summary>
    /// <remarks>
    /// This is what lets the backoff tests assert four growing delays without the suite
    /// spending fifteen seconds asleep.
    /// </remarks>
    internal sealed class FakeDelayProvider : IDelayProvider
    {
        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.FromResult(0);
        }
    }

    /// <summary>Runs the operation once, with no retry. Isolates callers from retry behaviour.</summary>
    internal sealed class PassThroughRetryExecutor : IRetryExecutor
    {
        public Task<T> ExecuteAsync<T>(
            string operationName,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);

        public Task ExecuteAsync(
            string operationName,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    /// <summary>A handler that returns a canned payload, or throws.</summary>
    internal sealed class StubHandler : ISyncTaskHandler
    {
        private readonly SyncPayload _payload;
        private readonly Exception? _failure;

        public StubHandler(string taskType, SyncPayload? payload = null, Exception? failure = null)
        {
            TaskType = taskType;
            _payload = payload ?? SyncPayload.Empty;
            _failure = failure;
        }

        public string TaskType { get; }

        public int Invocations { get; private set; }

        public SyncTaskRequest? LastTask { get; private set; }

        public Task<SyncPayload> ExecuteAsync(SyncTaskRequest task, CancellationToken cancellationToken)
        {
            Invocations++;
            LastTask = task;

            if (_failure != null)
            {
                throw _failure;
            }

            return Task.FromResult(_payload);
        }
    }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> driven by a delegate, so the platform client can be
    /// tested against exact status codes and bodies without a listening socket.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public List<string> RequestBodies { get; } = new List<string>();

        public static FakeHttpMessageHandler Returning(HttpStatusCode statusCode, string? body = null)
        {
            return new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(statusCode);

                if (body != null)
                {
                    response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                }

                return response;
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync().ConfigureAwait(false));

            return _responder(request);
        }
    }

    internal static class TestData
    {
        /// <summary>A well-formed ULID, matching what Ulid.NewUlid().ToString() produces.</summary>
        public const string ValidTaskId = "01JQFG8N3XRTV5KHW2YP4M7B6C";

        public static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero);

        public static SyncAgentOptions Options()
        {
            return new SyncAgentOptions
            {
                PlatformBaseUrl = new Uri("http://localhost:5100/"),
                ApiKey = "candidate-test-key-2026",
                ConnectionString = "Server=.;Database=AdventureWorks2025;Integrated Security=true",
                PollInterval = TimeSpan.FromSeconds(5),
                MaxIdleBackoff = TimeSpan.FromSeconds(60),
                MaxRecordsPerTask = 1000,
                MaxRetryAttempts = 4,
                RetryBaseDelay = TimeSpan.FromSeconds(1),
                MinModifiedSince = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ClockSkewTolerance = TimeSpan.FromMinutes(5),
            };
        }

        public static SyncTaskRequest Task(
            string taskType = "GetCustomers",
            string? taskId = null,
            string? modifiedSince = "2025-01-01T00:00:00Z",
            DateTimeOffset? createdAt = null)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (modifiedSince != null)
            {
                parameters["modifiedSince"] = modifiedSince;
            }

            return new SyncTaskRequest(
                taskId ?? ValidTaskId,
                taskType,
                parameters,
                createdAt ?? Now.AddMinutes(-1));
        }
    }
}
