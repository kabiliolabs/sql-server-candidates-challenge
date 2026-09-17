using Mediator;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Queries;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Dispatch;

/// <summary>Resolves the right query for a task's <c>taskType</c> from an allow-list and sends
/// it through Mediator. Never throws: any failure (unknown type, bad parameters, handler
/// exception) becomes a <see cref="SyncResult"/> with status "failed", so the caller can always
/// post a result back to the platform.</summary>
public sealed class SyncTaskDispatcher
{
    // The platform's taskType arrives as an untyped runtime string, but Mediator dispatch is
    // compile-time typed (ISender.Send<TResponse>(IRequest<TResponse>)) — this map is the one
    // place a new task type costs more than "one class, one DI line": one entry here,
    // constructing that task's query record from the validated request + configured row cap.
    private static readonly IReadOnlyDictionary<string, Func<ISender, SyncTaskRequest, int, CancellationToken, ValueTask<IReadOnlyList<object>>>> QuerySenders =
        new Dictionary<string, Func<ISender, SyncTaskRequest, int, CancellationToken, ValueTask<IReadOnlyList<object>>>>(StringComparer.Ordinal)
        {
            [TaskTypes.GetCustomers] = (sender, request, maxRows, ct) => sender.Send(new GetCustomersQuery(request.ModifiedSince, maxRows), ct),
            [TaskTypes.GetProducts] = (sender, request, maxRows, ct) => sender.Send(new GetProductsQuery(request.ModifiedSince, maxRows), ct),
            [TaskTypes.GetOrders] = (sender, request, maxRows, ct) => sender.Send(new GetOrdersQuery(request.ModifiedSince, maxRows), ct),
            [TaskTypes.GetProductInventory] = (sender, request, maxRows, ct) => sender.Send(new GetProductInventoryQuery(request.ModifiedSince, maxRows), ct),
        };

    private readonly ISender _sender;
    private readonly int _maxRowsPerTask;
    private readonly TimeProvider _timeProvider;

    public SyncTaskDispatcher(ISender sender, IOptions<SyncAgentOptions> options, TimeProvider? timeProvider = null)
    {
        _sender = sender;
        _maxRowsPerTask = options.Value.MaxRowsPerTask;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
    {
        if (!TaskTypes.All.Contains(task.TaskType) || !QuerySenders.TryGetValue(task.TaskType, out var send))
        {
            return SyncResult.Failed(task.TaskId, task.TaskType, $"Unknown or unsupported taskType: '{task.TaskType}'", _timeProvider.GetUtcNow());
        }

        if (!task.Parameters.TryGetValue("modifiedSince", out var rawModifiedSince) ||
            !DateTimeOffset.TryParse(rawModifiedSince, null, System.Globalization.DateTimeStyles.RoundtripKind, out var modifiedSince))
        {
            return SyncResult.Failed(task.TaskId, task.TaskType, "Missing or invalid 'modifiedSince' parameter", _timeProvider.GetUtcNow());
        }

        try
        {
            var request = new SyncTaskRequest(task.TaskId, modifiedSince);
            var data = await send(_sender, request, _maxRowsPerTask, cancellationToken);
            return SyncResult.Completed(task.TaskId, task.TaskType, data, _timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message, _timeProvider.GetUtcNow());
        }
    }
}
