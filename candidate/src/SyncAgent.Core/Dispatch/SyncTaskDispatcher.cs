using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Dispatch;

/// <summary>Resolves the right handler for a task's <c>taskType</c> from an allow-list and
/// executes it. Never throws: any failure (unknown type, bad parameters, handler exception)
/// becomes a <see cref="SyncResult"/> with status "failed", so the caller can always post a
/// result back to the platform.</summary>
public sealed class SyncTaskDispatcher
{
    private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlersByTaskType;
    private readonly TimeProvider _timeProvider;

    public SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> handlers, TimeProvider? timeProvider = null)
    {
        _handlersByTaskType = handlers.ToDictionary(h => h.TaskType, StringComparer.Ordinal);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
    {
        if (!TaskTypes.All.Contains(task.TaskType) || !_handlersByTaskType.TryGetValue(task.TaskType, out var handler))
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
            var data = await handler.ExecuteAsync(request, cancellationToken);
            return SyncResult.Completed(task.TaskId, task.TaskType, data, _timeProvider.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message, _timeProvider.GetUtcNow());
        }
    }
}
