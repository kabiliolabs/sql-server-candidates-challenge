using TaskExecutionService.Platform;

namespace TaskExecutionService.Sync;

/// <summary>Routes a task to the right executor and produces the result payload.</summary>
public interface ITaskDispatcher
{
    /// <summary>
    /// Validates and executes the given task, returning a completed or failed
    /// <see cref="SyncResult"/>. This never throws for task-level problems —
    /// failures are captured into a "failed" result so the polling loop can
    /// always report back to the platform.
    /// </summary>
    Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken);
}
