namespace SyncAgent.Worker.Platform;

/// <summary>
/// Talks to the central SyncPlatform's HTTP API. Wrapped behind an interface so the
/// Worker's polling loop can be unit tested against a fake, without a real HTTP server.
/// </summary>
public interface ISyncPlatformClient
{
    /// <summary>
    /// Retrieves the next pending sync task, or null if the queue is empty (204).
    /// </summary>
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the outcome of an executed task (success or failure) back to the platform.
    /// </summary>
    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default);
}
