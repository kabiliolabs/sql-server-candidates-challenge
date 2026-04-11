using SyncAgent.Models;

namespace SyncAgent.Services;

public interface IPlatformApiClient
{
    Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken = default);
    Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default);
}
