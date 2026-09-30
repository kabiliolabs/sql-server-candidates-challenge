using Microsoft.Extensions.Logging;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;

namespace SyncAgent.Core.Dispatching
{
    public sealed class SyncTaskDispatcher : ISyncTaskDispatcher
    {
        private const string GenericExecutionError = "Task execution failed. See agent logs for details.";

        private readonly IReadOnlyDictionary<string, ISyncTaskHandler> _handlers;
        private readonly IReadOnlySet<string> _supportedTaskTypes;
        private readonly ISyncTaskValidator _validator;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SyncTaskDispatcher> _logger;

        public SyncTaskDispatcher(
            IEnumerable<ISyncTaskHandler> handlers,
            ISyncTaskValidator validator,
            TimeProvider timeProvider,
            ILogger<SyncTaskDispatcher> logger)
        {
            _handlers = handlers.ToDictionary(h => h.TaskType, StringComparer.Ordinal);
            _supportedTaskTypes = _handlers.Keys.ToHashSet(StringComparer.Ordinal);
            _validator = validator;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
        {
            var executedAt = _timeProvider.GetUtcNow().UtcDateTime;

            var validation = _validator.Validate(task, _supportedTaskTypes);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Rejected task {TaskId}: {Error}", LogSanitizer.Sanitize(task.TaskId), validation.Error);
                return SyncResult.Failed(task, validation.Error!, executedAt);
            }

            try
            {
                var data = await _handlers[task.TaskType].ExecuteAsync(validation.Query!, cancellationToken);
                _logger.LogInformation("Task {TaskId} ({TaskType}) returned {Count} records",
                    task.TaskId, task.TaskType, data.Count);
                return SyncResult.Completed(task, data, executedAt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // agent is shutting down: not a task failure
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task {TaskId} ({TaskType}) failed", task.TaskId, task.TaskType);
                return SyncResult.Failed(task, GenericExecutionError, executedAt);
            }
        }
    }
}