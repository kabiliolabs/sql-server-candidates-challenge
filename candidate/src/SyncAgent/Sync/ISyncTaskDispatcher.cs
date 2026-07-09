using SyncAgent.Platform.Contracts;

namespace SyncAgent.Sync;

/// <summary>Validates a task, routes it to its handler, and turns the outcome into a postable result.</summary>
public interface ISyncTaskDispatcher
{
    /// <summary>
    /// Executes <paramref name="task"/> and returns the result to post.
    /// </summary>
    /// <returns>
    /// The result to POST, or <see langword="null"/> when the task carries no <c>taskId</c> or
    /// <c>taskType</c> and therefore cannot be reported on at all.
    /// </returns>
    /// <remarks>Never throws for a failing task: a failure is a "failed" result, not an exception.</remarks>
    Task<SyncResult?> DispatchAsync(SyncTask? task, CancellationToken cancellationToken);
}
