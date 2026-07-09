using SyncAgent.Platform.Contracts;

namespace SyncAgent.Platform;

/// <summary>
/// The agent's view of the central platform. Two calls, because the platform has two endpoints.
/// </summary>
public interface IPlatformClient
{
    /// <summary>Polls for the next queued task.</summary>
    /// <returns>The task, or <see langword="null"/> when the platform answers 204 No Content.</returns>
    /// <exception cref="HttpRequestException">The platform answered with a non-success status.</exception>
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken);

    /// <summary>Submits the outcome of an executed task.</summary>
    /// <exception cref="HttpRequestException">The platform rejected the result.</exception>
    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken);
}
