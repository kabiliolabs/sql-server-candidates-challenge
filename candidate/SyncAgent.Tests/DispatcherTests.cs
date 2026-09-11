using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SyncAgent.Domain;
using SyncAgent.Protocol;

namespace SyncAgent.Tests
{
    [TestFixture]
    public class DispatcherTests
    {
        private static SyncTaskDispatcher Empty()
        {
            return new SyncTaskDispatcher(Array.Empty<ISyncTaskHandler>(), NullLogger<SyncTaskDispatcher>.Instance);
        }

        [Test]
        public async Task UnknownTaskType_YieldsFailedResult()
        {
            SyncResult result = await Empty().DispatchAsync(TaskFactory.Create("t1", "Bogus"), CancellationToken.None);

            Assert.AreEqual("failed", result.status);
            Assert.AreEqual("t1", result.taskId);
            StringAssert.Contains("Unknown task type", result.errorMessage);
        }

        [Test]
        public async Task HandlerThrows_YieldsFailedResult_NoThrow()
        {
            var handler = new StubTaskHandler("GetProducts", (task, ct) =>
                throw new InvalidOperationException("db blew up"));
            var dispatcher = new SyncTaskDispatcher(new[] { handler }, NullLogger<SyncTaskDispatcher>.Instance);

            SyncResult result = await dispatcher.DispatchAsync(TaskFactory.Create("t2", "GetProducts"), CancellationToken.None);

            Assert.AreEqual("failed", result.status);
            Assert.AreEqual("db blew up", result.errorMessage);
        }

        [Test]
        public async Task HandlerCancellation_Propagates_NotConvertedToFailed()
        {
            var handler = new StubTaskHandler("GetProducts", (task, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                throw new OperationCanceledException(ct);
            });
            var dispatcher = new SyncTaskDispatcher(new[] { handler }, NullLogger<SyncTaskDispatcher>.Instance);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await dispatcher.DispatchAsync(TaskFactory.Create("t3", "GetProducts"), cts.Token));
            }
        }

        [Test]
        public async Task Success_PassesCompletedResultThrough()
        {
            var handler = new StubTaskHandler("GetOrders", (task, ct) =>
                Task.FromResult(SyncResult.Completed(task.taskId, task.taskType,
                    new List<object> { new { orderId = 1 } }, DateTime.UtcNow)));
            var dispatcher = new SyncTaskDispatcher(new[] { handler }, NullLogger<SyncTaskDispatcher>.Instance);

            SyncResult result = await dispatcher.DispatchAsync(TaskFactory.Create("t4", "GetOrders"), CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.AreEqual(1, result.recordCount);
        }
    }
}