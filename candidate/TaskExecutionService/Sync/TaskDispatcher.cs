using TaskExecutionService.Platform;

namespace TaskExecutionService.Sync;

/// <summary>
/// Default dispatcher. Builds a lookup of executors keyed by their
/// <see cref="ISyncTaskExecutor.TaskType"/> and routes each task to the matching
/// one. All failures are turned into a "failed" <see cref="SyncResult"/> so the
/// worker can always report an outcome to the platform.
/// </summary>
public sealed class TaskDispatcher : ITaskDispatcher
{
    private readonly IReadOnlyDictionary<string, ISyncTaskExecutor> _executors;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TaskDispatcher> _logger;

    public TaskDispatcher(
        IEnumerable<ISyncTaskExecutor> executors,
        TimeProvider timeProvider,
        ILogger<TaskDispatcher> logger)
    {
        _executors = executors.ToDictionary(e => e.TaskType, StringComparer.Ordinal);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken)
    {
        // Guard: a task must at least carry an id and type to be answerable.
        if (string.IsNullOrWhiteSpace(task.TaskId) || string.IsNullOrWhiteSpace(task.TaskType))
        {
            _logger.LogWarning("Received a task with missing taskId or taskType; skipping.");
            return SyncResult.Failed(task.TaskId, task.TaskType, "Task is missing taskId or taskType.", Now());
        }

        try
        {
            if (!_executors.TryGetValue(task.TaskType, out var executor))
            {
                throw new SyncValidationException($"Unknown task type '{task.TaskType}'.");
            }

            var parameters = SyncParameters.Parse(task.Parameters);
            var data = await executor.ExecuteAsync(parameters, cancellationToken);

            _logger.LogInformation(
                "Task {TaskType} ({TaskId}) completed with {RecordCount} record(s).",
                task.TaskType, task.TaskId, data.Count);

            return SyncResult.Completed(task.TaskId, task.TaskType, data, Now());
        }
        catch (SyncValidationException ex)
        {
            // Input problem: the message is safe to return to the platform.
            _logger.LogWarning("Task {TaskType} ({TaskId}) rejected: {Reason}", task.TaskType, task.TaskId, ex.Message);
            return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message, Now());
        }
        catch (OperationCanceledException)
        {
            throw; // shutdown in progress — let the loop stop cleanly.
        }
        catch (Exception ex)
        {
            // Unexpected failure: log full detail locally, return a generic message.
            _logger.LogError(ex, "Task {TaskType} ({TaskId}) failed unexpectedly.", task.TaskType, task.TaskId);
            return SyncResult.Failed(task.TaskId, task.TaskType, "Task execution failed due to an internal error.", Now());
        }
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
