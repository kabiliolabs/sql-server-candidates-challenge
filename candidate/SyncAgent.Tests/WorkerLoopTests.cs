using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace SyncAgent.Tests
{
    [TestFixture]
    public class WorkerLoopTests
    {
        // Short backoff so loop iterations are fast in tests; one test also
        // proves cancellation interrupts a long backoff promptly.
        private static AgentOptions Options(int pollMs = 50)
        {
            return new AgentOptions
            {
                ConnectionString = "Server=.;Database=x;User Id=u;Password=p;",
                Url = "http://localhost:5100",
                ApiKey = "key",
                PollIntervalMs = pollMs,
                MaxRows = 1000,
                CommandTimeoutSeconds = 10
            };
        }

        private static SyncAgentWorker Worker(FakePlatformClient platform, FakeDispatcher dispatcher, int pollMs = 50)
        {
            return new SyncAgentWorker(platform, dispatcher, Options(pollMs),
                NullLogger<SyncAgentWorker>.Instance);
        }

        [Test]
        public async Task DuplicateTaskId_ProcessedOnce()
        {
            var platform = new FakePlatformClient();
            platform.Tasks.Enqueue(TaskFactory.Create("id-1", "GetCustomers"));
            platform.Tasks.Enqueue(TaskFactory.Create("id-1", "GetCustomers"));
            var dispatcher = new FakeDispatcher();
            SyncAgentWorker worker = Worker(platform, dispatcher);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(600);
                await worker.RunLoopAsync(cts.Token); // returns cleanly on cancellation
            }

            Assert.AreEqual(1, dispatcher.DispatchCount,
                "duplicate taskId must be dispatched exactly once (dedupe)");
        }

        [Test]
        public async Task LoopSurvivesClientFailure_AndStopsOnCancel()
        {
            var platform = new FakePlatformClient { ThrowOnGetNext = true };
            var dispatcher = new FakeDispatcher();
            SyncAgentWorker worker = Worker(platform, dispatcher);

            var sw = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(300);
                await worker.RunLoopAsync(cts.Token);
            }
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 3000,
                "loop must keep running through exceptions and stop promptly on cancel");
        }

        [Test]
        public async Task CancelDuringBackoff_ExitsPromptly()
        {
            // Long backoff: proves cancellation interrupts the idle delay, not just the HTTP wait.
            var platform = new FakePlatformClient();
            var dispatcher = new FakeDispatcher();
            SyncAgentWorker worker = Worker(platform, dispatcher, pollMs: 30000);

            var sw = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(150);
                await worker.RunLoopAsync(cts.Token);
            }
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 3000,
                "cancellation must interrupt a long poll backoff");
        }
    }
}