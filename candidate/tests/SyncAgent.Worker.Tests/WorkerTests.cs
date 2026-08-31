using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SyncAgent.Worker.Configuration;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks;

namespace SyncAgent.Worker.Tests;

public class WorkerTests
{
    private sealed class FakePlatformClient : ISyncPlatformClient
    {
        public SyncResult? LastPostedResult { get; private set; }

        public Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("ProcessTaskAsync does not poll - not exercised by these tests.");

        public Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default)
        {
            LastPostedResult = result;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDispatcher(Func<SyncTask, object> onDispatch) : ISyncTaskDispatcher
    {
        public Task<object> DispatchAsync(SyncTask task, CancellationToken cancellationToken) =>
            Task.FromResult(onDispatch(task));
    }

    private static Worker CreateWorker(ISyncPlatformClient platformClient, ISyncTaskDispatcher dispatcher) => new(
        platformClient,
        dispatcher,
        Options.Create(new SyncPlatformOptions { BaseUrl = "http://localhost", ApiKey = "test-key" }),
        NullLogger<Worker>.Instance);

    private static SyncTask MakeTask() => new()
    {
        TaskId = "task-1",
        TaskType = SyncTaskType.GetCustomers,
        Parameters = null,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ProcessTaskAsync_PostsCompletedResult_WithDataAndCount_OnSuccess()
    {
        var platformClient = new FakePlatformClient();
        var data = new List<string> { "a", "b", "c" };
        var dispatcher = new FakeDispatcher(_ => data);
        var worker = CreateWorker(platformClient, dispatcher);
        var task = MakeTask();

        await worker.ProcessTaskAsync(task, CancellationToken.None);

        var result = platformClient.LastPostedResult;
        Assert.NotNull(result);
        Assert.Equal("completed", result!.Status);
        Assert.Same(data, result.Data);
        Assert.Equal(3, result.RecordCount);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
    }

    [Fact]
    public async Task ProcessTaskAsync_PostsFailedResult_WithErrorMessage_WhenDispatchThrows()
    {
        var platformClient = new FakePlatformClient();
        var dispatcher = new FakeDispatcher(_ => throw new InvalidOperationException("query failed"));
        var worker = CreateWorker(platformClient, dispatcher);
        var task = MakeTask();

        await worker.ProcessTaskAsync(task, CancellationToken.None);

        var result = platformClient.LastPostedResult;
        Assert.NotNull(result);
        Assert.Equal("failed", result!.Status);
        Assert.Null(result.Data);
        Assert.Equal(0, result.RecordCount);
        Assert.Equal("query failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessTaskAsync_DoesNotSwallowCancellation()
    {
        var platformClient = new FakePlatformClient();
        var dispatcher = new FakeDispatcher(_ => throw new OperationCanceledException());
        var worker = CreateWorker(platformClient, dispatcher);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => worker.ProcessTaskAsync(MakeTask(), CancellationToken.None));

        // A cancellation should propagate for the host to observe during shutdown,
        // not be reported to the platform as a "failed" task result.
        Assert.Null(platformClient.LastPostedResult);
    }
}
