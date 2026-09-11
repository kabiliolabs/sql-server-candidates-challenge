using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Domain;
using SyncAgent.Protocol;

namespace SyncAgent.Tests
{
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }

    /// <summary>Handler for dispatcher unit tests.</summary>
    internal sealed class StubTaskHandler : ISyncTaskHandler
    {
        private readonly Func<SyncTask, CancellationToken, Task<SyncResult>> _execute;

        public StubTaskHandler(string taskType, Func<SyncTask, CancellationToken, Task<SyncResult>> execute)
        {
            TaskType = taskType;
            _execute = execute;
        }

        public string TaskType { get; }

        public Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken)
        {
            return _execute(task, cancellationToken);
        }
    }

    internal sealed class FakePlatformClient : SyncPlatformClient
    {
        public readonly Queue<SyncTask> Tasks = new Queue<SyncTask>();
        public bool ThrowOnGetNext;

        public FakePlatformClient()
            : base(new HttpClient(), NullLogger<SyncPlatformClient>.Instance)
        {
        }

        public override Task<SyncTask> GetNextTaskAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnGetNext)
                throw new InvalidOperationException("platform unavailable");
            return Task.FromResult(Tasks.Count > 0 ? Tasks.Dequeue() : null);
        }

        public override Task<bool> PostResultAsync(SyncResult result, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }

    internal sealed class FakeDispatcher : SyncTaskDispatcher
    {
        public int DispatchCount;

        public FakeDispatcher()
            : base(Array.Empty<ISyncTaskHandler>(), NullLogger<SyncTaskDispatcher>.Instance)
        {
        }

        public override Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
        {
            DispatchCount++;
            return Task.FromResult(
                SyncResult.Completed(task.taskId, task.taskType, new List<object>(), DateTime.UtcNow));
        }
    }

    internal static class TaskFactory
    {
        public static SyncTask Create(string id, string type)
        {
            return new SyncTask
            {
                taskId = id,
                taskType = type,
                parameters = null,
                createdAt = DateTime.UtcNow
            };
        }
    }
}