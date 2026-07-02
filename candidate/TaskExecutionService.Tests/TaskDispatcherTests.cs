using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskExecutionService.Platform;
using TaskExecutionService.Sync;
using TaskExecutionService.Tests.TestDoubles;
using Xunit;

namespace TaskExecutionService.Tests;

public sealed class TaskDispatcherTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 12, 10, 30, 5, TimeSpan.Zero);

    private static SyncTask TaskOf(string type) => new()
    {
        TaskId = "01JQFG8N3XRTV5KHW2YP4M7B6C",
        TaskType = type,
        Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" },
    };

    private static TaskDispatcher CreateDispatcher(params ISyncTaskExecutor[] executors) =>
        new(executors, new FixedTimeProvider(FixedNow), NullLogger<TaskDispatcher>.Instance);

    [Fact]
    public async Task Dispatch_RoutesToMatchingExecutor_AndCompletes()
    {
        var rows = new object[] { new { customerId = 1 }, new { customerId = 2 } };
        var customers = new FakeExecutor("GetCustomers", _ => rows);
        var products = new FakeExecutor("GetProducts", _ => Array.Empty<object>());
        var dispatcher = CreateDispatcher(customers, products);

        var result = await dispatcher.DispatchAsync(TaskOf("GetCustomers"), CancellationToken.None);

        customers.WasCalled.Should().BeTrue();
        products.WasCalled.Should().BeFalse();
        result.Status.Should().Be(SyncResult.StatusCompleted);
        result.RecordCount.Should().Be(2);
        result.Data.Should().BeSameAs(rows);
        result.ErrorMessage.Should().BeNull();
        result.ExecutedAt.Should().Be(FixedNow.UtcDateTime);
    }

    [Fact]
    public async Task Dispatch_UnknownTaskType_ReturnsFailed()
    {
        var dispatcher = CreateDispatcher(new FakeExecutor("GetCustomers", _ => Array.Empty<object>()));

        var result = await dispatcher.DispatchAsync(TaskOf("GetUnicorns"), CancellationToken.None);

        result.Status.Should().Be(SyncResult.StatusFailed);
        result.Data.Should().BeNull();
        result.RecordCount.Should().Be(0);
        result.ErrorMessage.Should().Contain("GetUnicorns");
    }

    [Fact]
    public async Task Dispatch_InvalidParameters_ReturnsFailedWithSafeMessage()
    {
        var dispatcher = CreateDispatcher(new FakeExecutor("GetCustomers", _ => Array.Empty<object>()));
        var task = TaskOf("GetCustomers");
        task.Parameters = new Dictionary<string, string>(); // missing modifiedSince

        var result = await dispatcher.DispatchAsync(task, CancellationToken.None);

        result.Status.Should().Be(SyncResult.StatusFailed);
        result.ErrorMessage.Should().Contain("modifiedSince");
    }

    [Fact]
    public async Task Dispatch_ExecutorThrows_ReturnsGenericFailure()
    {
        var boom = new FakeExecutor("GetCustomers", _ => throw new InvalidOperationException("secret db detail"));
        var dispatcher = CreateDispatcher(boom);

        var result = await dispatcher.DispatchAsync(TaskOf("GetCustomers"), CancellationToken.None);

        result.Status.Should().Be(SyncResult.StatusFailed);
        result.ErrorMessage.Should().NotContain("secret db detail"); // internal detail must not leak
        result.ErrorMessage.Should().Contain("internal error");
    }

    [Fact]
    public async Task Dispatch_MissingTaskId_ReturnsFailed()
    {
        var dispatcher = CreateDispatcher(new FakeExecutor("GetCustomers", _ => Array.Empty<object>()));
        var task = TaskOf("GetCustomers");
        task.TaskId = "";

        var result = await dispatcher.DispatchAsync(task, CancellationToken.None);

        result.Status.Should().Be(SyncResult.StatusFailed);
    }
}
