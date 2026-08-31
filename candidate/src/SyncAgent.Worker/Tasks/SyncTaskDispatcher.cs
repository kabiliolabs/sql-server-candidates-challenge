using SyncAgent.Worker.Platform;

namespace SyncAgent.Worker.Tasks;

public class SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> handlers) : ISyncTaskDispatcher
{
    private readonly IReadOnlyDictionary<SyncTaskType, ISyncTaskHandler> _handlersByType = handlers.ToDictionary(h => h.TaskType);

    public Task<object> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
    {
        if (!_handlersByType.TryGetValue(task.TaskType, out var handler))
        {
            throw new NotSupportedException($"No handler registered for task type '{task.TaskType}'.");
        }

        return handler.ExecuteAsync(task.Parameters, cancellationToken);
    }
}
