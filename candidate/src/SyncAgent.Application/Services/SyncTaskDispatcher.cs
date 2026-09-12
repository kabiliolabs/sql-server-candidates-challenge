using Microsoft.Extensions.Logging;
using SyncAgent.Application.Abstractions;

namespace SyncAgent.Application.Services;

public interface ISyncTaskDispatcher
{
    Task<SyncTaskExecutionResult> DispatchAsync(
        string taskType,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken);
}

public sealed class SyncTaskDispatcher : ISyncTaskDispatcher
{
    private readonly Dictionary<string, ISyncTaskHandler> _handlers;
    private readonly ILogger<SyncTaskDispatcher> _logger;

    public SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> handlers, ILogger<SyncTaskDispatcher> logger)
    {
        // Task types are matched exactly, the way the platform emits and compares them.
        _handlers = handlers.ToDictionary(handler => handler.TaskType, StringComparer.Ordinal);
        _logger = logger;
    }

    public async Task<SyncTaskExecutionResult> DispatchAsync(
        string taskType,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(taskType, out var handler))
        {
            // Reported back as a failed task so the platform learns the outcome of the task it sent.
            _logger.LogWarning("No handler is registered for task type {TaskType}", taskType);
            return SyncTaskExecutionResult.Failed($"Unknown task type: {taskType}");
        }

        return await handler.ExecuteAsync(parameters, cancellationToken);
    }
}
