using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncAgent.Domain;

namespace SyncAgent.Protocol
{
    /// <summary>Routes tasks to handlers by task type; unknown type or handler failure -->
    /// "failed" result. Never throws (except cancellation).</summary>
    public sealed class SyncTaskDispatcher
    {
        private readonly Dictionary<string, ISyncTaskHandler> _handlers;
        private readonly ILogger<SyncTaskDispatcher> _logger;

        public SyncTaskDispatcher(IEnumerable<ISyncTaskHandler> handlers, ILogger<SyncTaskDispatcher> logger)
        {
            _handlers = handlers.ToDictionary(h => h.TaskType);
            _logger = logger;
        }

        public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
        {
            ISyncTaskHandler handler;
            if (!_handlers.TryGetValue(task.taskType, out handler))
            {
                _logger.LogWarning("No handler registered for task type '{TaskType}'.", task.taskType);
                return SyncResult.Failed(task.taskId, task.taskType,
                    "Unknown task type: " + task.taskType, DateTime.UtcNow);
            }

            try
            {
                return await handler.ExecuteAsync(task, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // cancellation is not a handler failure — let the loop exit promptly
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler {TaskType} failed for task {TaskId}.", task.taskType, task.taskId);
                return SyncResult.Failed(task.taskId, task.taskType, ex.Message, DateTime.UtcNow);
            }
        }
    }
}