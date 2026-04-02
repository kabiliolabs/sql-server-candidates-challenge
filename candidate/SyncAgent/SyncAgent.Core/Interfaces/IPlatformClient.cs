using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

public interface IPlatformClient
{
    Task<SyncTask?> GetNextTaskAsync(CancellationToken ct = default);
    Task PostResultAsync(SyncResult result, CancellationToken ct = default);
}
