using SyncAgent.Handlers;
using SyncAgent.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Services;

public sealed class SyncTaskDispatcher : ISyncTaskDispatcher
{
    private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlers;

    public SyncTaskDispatcher(
        IEnumerable<ISyncTaskHandler> handlers)
    {
        _handlers = handlers.ToDictionary(
            handler => handler.TaskType,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.TaskType))
        {
            throw new ArgumentException(
                "Task type cannot be empty.",
                nameof(task));
        }

        if (!_handlers.TryGetValue(task.TaskType, out var handler))
        {
            throw new NotSupportedException(
                $"Unsupported task type: {task.TaskType}");
        }

        return handler.ExecuteAsync(
            task,
            cancellationToken);
    }
}