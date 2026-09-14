using SyncAgent.Contracts;

namespace SyncAgent.Api;

/// <summary>
/// Client for the SyncPlatform HTTP server: fetches pending tasks and posts results.
/// </summary>
public interface ISyncPlatformApiClient
{
    /// <summary>
    /// Fetches the next pending sync task, or null if the queue is empty (204).
    /// </summary>
    Task<SyncTaskDto?> GetNextTaskAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Posts the result of a processed sync task back to the platform.
    /// </summary>
    Task PostResultAsync(SyncResultDto result, CancellationToken cancellationToken);
}
