using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Queries;
using SyncAgent.Core.Tasks;
using Xunit;

namespace SyncAgent.Core.Tests.Dispatch;

public class SyncTaskDispatcherTests
{
    private static SyncTask MakeTask(string taskType, string? modifiedSince = "2025-01-01T00:00:00Z") =>
        new()
        {
            TaskId = "task-1",
            TaskType = taskType,
            Parameters = modifiedSince is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["modifiedSince"] = modifiedSince },
            CreatedAt = DateTimeOffset.UtcNow
        };

    /// <summary>Builds a dispatcher backed by a real Mediator pipeline (not a hand-rolled fake
    /// ISender) — exercises the exact same handler-resolution path production uses, just with
    /// test-only handlers overriding the real repository-backed ones for the request type under
    /// test. AddMediator() (generated inside the Core assembly) auto-registers all four *real*
    /// handlers it finds there, and resolving ISender eagerly builds its dispatch table — so
    /// every real handler's constructor dependencies must be satisfiable even in a test that
    /// only exercises one of them; these no-op repositories exist purely to make that possible,
    /// not because the test cares what they return.</summary>
    private static SyncTaskDispatcher CreateDispatcher(Action<IServiceCollection>? registerHandlers = null, int maxRows = 5000)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISyncQueryRepository<CustomerRecord>>(new EmptyRepository<CustomerRecord>());
        services.AddSingleton<ISyncQueryRepository<ProductRecord>>(new EmptyRepository<ProductRecord>());
        services.AddSingleton<ISyncQueryRepository<OrderRecord>>(new EmptyRepository<OrderRecord>());
        services.AddSingleton<ISyncQueryRepository<ProductInventoryRecord>>(new EmptyRepository<ProductInventoryRecord>());
        services.AddMediator();
        registerHandlers?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new SyncTaskDispatcher(provider.GetRequiredService<ISender>(), Options.Create(new SyncAgentOptions { MaxRowsPerTask = maxRows }));
    }

    private sealed class EmptyRepository<TRecord> : ISyncQueryRepository<TRecord>
    {
        public Task<IReadOnlyList<TRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TRecord>>([]);
    }

    [Fact]
    public async Task DispatchAsync_UnknownTaskType_ReturnsFailedWithoutThrowing()
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(MakeTask("DeleteEverything"), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Failed, result.Status);
        Assert.Null(result.Data);
        Assert.Equal(0, result.RecordCount);
        Assert.Contains("Unknown or unsupported taskType", result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_MissingModifiedSince_ReturnsFailed()
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(MakeTask(TaskTypes.GetCustomers, modifiedSince: null), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Failed, result.Status);
        Assert.Contains("modifiedSince", result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_MalformedModifiedSince_ReturnsFailed()
    {
        var dispatcher = CreateDispatcher();

        var result = await dispatcher.DispatchAsync(MakeTask(TaskTypes.GetCustomers, modifiedSince: "not-a-date"), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Failed, result.Status);
        Assert.Contains("modifiedSince", result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ReturnsFailedInsteadOfPropagating()
    {
        var dispatcher = CreateDispatcher(services =>
            services.AddSingleton<IRequestHandler<GetCustomersQuery, IReadOnlyList<object>>>(
                new ThrowingHandler<GetCustomersQuery>(new InvalidOperationException("SQL connection refused"))));

        var result = await dispatcher.DispatchAsync(MakeTask(TaskTypes.GetCustomers), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Failed, result.Status);
        Assert.Equal("SQL connection refused", result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_HandlerSucceeds_ReturnsCompletedWithMatchingRecordCount()
    {
        IReadOnlyList<object> data = [new { Id = 1 }, new { Id = 2 }, new { Id = 3 }];
        var dispatcher = CreateDispatcher(services =>
            services.AddSingleton<IRequestHandler<GetProductsQuery, IReadOnlyList<object>>>(new CannedHandler<GetProductsQuery>(data)));

        var result = await dispatcher.DispatchAsync(MakeTask(TaskTypes.GetProducts), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Completed, result.Status);
        Assert.Equal(3, result.RecordCount);
        Assert.Equal(data.Count, result.Data!.Count);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_MultipleHandlersRegistered_ResolvesTheMatchingOneByTaskType()
    {
        var dispatcher = CreateDispatcher(services =>
        {
            services.AddSingleton<IRequestHandler<GetCustomersQuery, IReadOnlyList<object>>>(
                new CannedHandler<GetCustomersQuery>([new { Kind = "customer" }]));
            services.AddSingleton<IRequestHandler<GetProductsQuery, IReadOnlyList<object>>>(
                new CannedHandler<GetProductsQuery>([new { Kind = "product" }, new { Kind = "product" }]));
        });

        var result = await dispatcher.DispatchAsync(MakeTask(TaskTypes.GetProducts), CancellationToken.None);

        Assert.Equal(SyncResultStatus.Completed, result.Status);
        Assert.Equal(2, result.RecordCount);
    }

    private sealed class CannedHandler<TRequest>(IReadOnlyList<object> data) : IRequestHandler<TRequest, IReadOnlyList<object>>
        where TRequest : IRequest<IReadOnlyList<object>>
    {
        public ValueTask<IReadOnlyList<object>> Handle(TRequest message, CancellationToken cancellationToken) =>
            ValueTask.FromResult(data);
    }

    private sealed class ThrowingHandler<TRequest>(Exception exception) : IRequestHandler<TRequest, IReadOnlyList<object>>
        where TRequest : IRequest<IReadOnlyList<object>>
    {
        public ValueTask<IReadOnlyList<object>> Handle(TRequest message, CancellationToken cancellationToken) =>
            throw exception;
    }
}
