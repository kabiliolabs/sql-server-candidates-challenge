using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    public interface IPlatformClient
    {
        Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken);

        Task PostResultAsync(SyncResult result, CancellationToken cancellationToken);
    }
}