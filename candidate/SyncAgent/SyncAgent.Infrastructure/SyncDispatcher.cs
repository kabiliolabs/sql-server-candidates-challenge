using Microsoft.Extensions.Logging;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure;

public class SyncDispatcher
{
    private readonly Dictionary<string, ISyncTaskHandler> _handlers;
    private readonly ILogger<SyncDispatcher> _logger;

    public SyncDispatcher(IEnumerable<ISyncTaskHandler> handlers, ILogger<SyncDispatcher> logger)
    {
        _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken ct = default)
    {
        if (!_handlers.TryGetValue(task.TaskType, out var handler))
        {
            _logger.LogWarning("No handler registered for task type: {TaskType}", task.TaskType);
            return new SyncResult
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "failed",
                ErrorMessage = $"Unknown task type: {task.TaskType}"
            };
        }

        try
        {
            _logger.LogInformation("Handling task {TaskType} ({TaskId})", task.TaskType, task.TaskId);
            var result = await handler.HandleAsync(task, ct);
            _logger.LogInformation("Completed {TaskType} ({TaskId}) — {RecordCount} records",
                task.TaskType, task.TaskId, result.RecordCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler failed for {TaskType} ({TaskId})", task.TaskType, task.TaskId);
            return new SyncResult
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "failed",
                ErrorMessage = ex.Message
            };
        }
    }
}
