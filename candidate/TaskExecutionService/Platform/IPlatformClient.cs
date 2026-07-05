namespace TaskExecutionService.Platform;

/// <summary>Abstraction over the platform HTTP API, so the worker can be unit-tested.</summary>
public interface IPlatformClient
{
    /// <summary>
    /// Fetches the next pending task, or null when the queue is empty (HTTP 204).
    /// </summary>
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken);

    /// <summary>Posts an executed task result back to the platform.</summary>
    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken);
}
