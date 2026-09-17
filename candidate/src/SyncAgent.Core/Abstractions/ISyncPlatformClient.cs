using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Abstractions;

public interface ISyncPlatformClient
{
    /// <summary>Returns the next queued task, or null when the queue is empty (204).</summary>
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken);

    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken);
}
