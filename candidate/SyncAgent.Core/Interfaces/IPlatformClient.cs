using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

public interface IPlatformClient
{
    /// <summary>Returns the next pending task, or null if the queue is empty (204).</summary>
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken);

    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken);
}
