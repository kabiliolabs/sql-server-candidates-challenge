using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Dispatching;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Dispatching;

public sealed class SyncTaskDispatcherTests
{
    private static SyncTaskDispatcher CreateDispatcher(params ISyncTaskHandler[] handlers)
    {
        var timeProvider = new FixedTimeProvider(TestData.Now);
        return new SyncTaskDispatcher(
            handlers,
            new SyncTaskValidator(timeProvider),
            timeProvider,
            NullLogger<SyncTaskDispatcher>.Instance);
    }

    [Fact]
    public async Task Valid_task_is_executed_by_matching_handler_and_returns_completed_result()
    {
        var records = new object[] { new { id = 1 }, new { id = 2 } };
        var handler = FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers, records);
        var dispatcher = CreateDispatcher(handler);

        var result = await dispatcher.DispatchAsync(TestData.Task(), CancellationToken.None);

        handler.Calls.ShouldBe(1);
        handler.LastQuery!.ModifiedSinceUtc.ShouldBe(TestData.ModifiedSince);

        result.Status.ShouldBe(SyncStatus.Completed);
        result.TaskId.ShouldBe(TestData.ValidTaskId);
        result.TaskType.ShouldBe(SyncTaskTypes.GetCustomers);
        result.Data.ShouldBeSameAs(records);
        result.RecordCount.ShouldBe(2);
        result.ErrorMessage.ShouldBeNull();
        result.ExecutedAt.ShouldBe(TestData.Now.UtcDateTime);
        result.ExecutedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Task_is_routed_only_to_the_handler_for_its_type()
    {
        var customers = FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers);
        var products = FakeSyncTaskHandler.Returning(SyncTaskTypes.GetProducts);
        var dispatcher = CreateDispatcher(customers, products);

        await dispatcher.DispatchAsync(TestData.Task(taskType: SyncTaskTypes.GetProducts), CancellationToken.None);

        products.Calls.ShouldBe(1);
        customers.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Empty_result_is_completed_with_zero_records()
    {
        var dispatcher = CreateDispatcher(FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers));

        var result = await dispatcher.DispatchAsync(TestData.Task(), CancellationToken.None);

        result.Status.ShouldBe(SyncStatus.Completed);
        result.Data.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
        result.RecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task Unsupported_task_type_returns_failed_result_without_calling_any_handler()
    {
        var handler = FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers);
        var dispatcher = CreateDispatcher(handler);

        var result = await dispatcher.DispatchAsync(TestData.Task(taskType: "Unknown"), CancellationToken.None);

        handler.Calls.ShouldBe(0);
        result.Status.ShouldBe(SyncStatus.Failed);
        result.ErrorMessage.ShouldBe("Unsupported taskType.");
        result.Data.ShouldBeNull();
        result.RecordCount.ShouldBe(0);
    }

    [Fact]
    public async Task Invalid_parameters_return_failed_result_without_calling_the_handler()
    {
        var handler = FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers);
        var dispatcher = CreateDispatcher(handler);

        var result = await dispatcher.DispatchAsync(TestData.Task(withParameters: false), CancellationToken.None);

        handler.Calls.ShouldBe(0);
        result.Status.ShouldBe(SyncStatus.Failed);
    }

    [Fact]
    public async Task Handler_exception_returns_generic_error_without_leaking_details()
    {
        var handler = FakeSyncTaskHandler.Throwing(
            SyncTaskTypes.GetCustomers,
            new InvalidOperationException("Login failed for user 'sa' on server PROD-SQL-01"));
        var dispatcher = CreateDispatcher(handler);

        var result = await dispatcher.DispatchAsync(TestData.Task(), CancellationToken.None);

        result.Status.ShouldBe(SyncStatus.Failed);
        result.Data.ShouldBeNull();
        result.RecordCount.ShouldBe(0);
        result.ErrorMessage.ShouldBe("Task execution failed. See agent logs for details.");
        result.ErrorMessage.ShouldNotContain("PROD-SQL-01");
    }

    [Fact]
    public async Task Cancellation_during_shutdown_is_propagated_not_reported_as_failure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = new FakeSyncTaskHandler(SyncTaskTypes.GetCustomers, (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>());
        });
        var dispatcher = CreateDispatcher(handler);

        await Should.ThrowAsync<OperationCanceledException>(() => dispatcher.DispatchAsync(TestData.Task(), cts.Token));
    }

    [Fact]
    public async Task Cancellation_not_caused_by_shutdown_is_reported_as_failure()
    {
        // e.g. an internal timeout inside the handler: the agent keeps running, the task fails
        var handler = FakeSyncTaskHandler.Throwing(SyncTaskTypes.GetCustomers, new OperationCanceledException());
        var dispatcher = CreateDispatcher(handler);

        var result = await dispatcher.DispatchAsync(TestData.Task(), CancellationToken.None);

        result.Status.ShouldBe(SyncStatus.Failed);
    }

    [Fact]
    public void Duplicate_handlers_for_the_same_task_type_fail_fast()
    {
        Should.Throw<ArgumentException>(() => CreateDispatcher(
            FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers),
            FakeSyncTaskHandler.Returning(SyncTaskTypes.GetCustomers)));
    }
}
