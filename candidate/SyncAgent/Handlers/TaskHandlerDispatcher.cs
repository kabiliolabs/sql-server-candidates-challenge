using SyncAgent.Contracts;

namespace SyncAgent.Handlers;

/// <summary>
/// Resolves the appropriate ITaskHandler for a given task's taskType and dispatches to it.
/// Unknown task types produce a graceful failed result instead of an unhandled exception,
/// so a new/unrecognized task type never crashes the polling worker.
/// </summary>
public sealed class TaskHandlerDispatcher(IEnumerable<ITaskHandler> handlers, ILogger<TaskHandlerDispatcher> logger)
{
    private readonly Dictionary<string, ITaskHandler> _handlersByType =
        handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);

    public async Task<SyncResultDto> DispatchAsync(SyncTaskDto task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!string.IsNullOrWhiteSpace(task.TaskType) && _handlersByType.TryGetValue(task.TaskType, out var handler))
        {
            return await handler.HandleAsync(task, cancellationToken);
        }

        logger.LogWarning("No handler registered for task type {TaskType} (task {TaskId})", task.TaskType, task.TaskId);

        return new SyncResultDto
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = "failed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = $"No handler registered for task type '{task.TaskType}'."
        };
    }
}
