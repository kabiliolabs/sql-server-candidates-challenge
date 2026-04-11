namespace SyncAgent.Services;

// Resolves the correct handler for a given task type.
// To add a new task type: implement ITaskHandler and register it in DI — no changes needed here.
public class TaskHandlerFactory : ITaskHandlerFactory
{
    private readonly IReadOnlyDictionary<string, ITaskHandler> _handlers;

    public TaskHandlerFactory(IEnumerable<ITaskHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);
    }

    public ITaskHandler GetHandler(string taskType)
    {
        if (!_handlers.TryGetValue(taskType, out var handler))
            throw new NotSupportedException($"No handler registered for task type '{taskType}'.");
        return handler;
    }

    public bool HasHandler(string taskType) =>
        _handlers.ContainsKey(taskType);
}
