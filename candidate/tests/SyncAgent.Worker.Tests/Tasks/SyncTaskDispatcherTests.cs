using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks;

namespace SyncAgent.Worker.Tests.Tasks;

public class SyncTaskDispatcherTests
{
    private sealed class FakeHandler(SyncTaskType taskType, object result) : ISyncTaskHandler
    {
        public SyncTaskType TaskType { get; } = taskType;

        public Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static SyncTask MakeTask(SyncTaskType taskType) => new()
    {
        TaskId = "task-1",
        TaskType = taskType,
        Parameters = null,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task DispatchAsync_RoutesToTheHandlerMatchingTaskType()
    {
        var customersResult = new List<string> { "customer-a" };
        var productsResult = new List<string> { "product-a" };
        var dispatcher = new SyncTaskDispatcher(
        [
            new FakeHandler(SyncTaskType.GetCustomers, customersResult),
            new FakeHandler(SyncTaskType.GetProducts, productsResult)
        ]);

        var result = await dispatcher.DispatchAsync(MakeTask(SyncTaskType.GetProducts), CancellationToken.None);

        Assert.Same(productsResult, result);
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenNoHandlerRegisteredForTaskType()
    {
        var dispatcher = new SyncTaskDispatcher([]);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.DispatchAsync(MakeTask(SyncTaskType.GetOrders), CancellationToken.None));

        Assert.Contains("GetOrders", ex.Message);
    }
}
