using SyncTaskWorkerService.Models;

namespace SyncTaskWorkerService.CentralPlatformHttpClient
{
    public interface IPlatformHttpClient
    {
        Task<SyncTask?> GetTaskAsync(CancellationToken cancellationToken);

        Task<string> PostResultAsync(SyncResult result, CancellationToken cancellationToken);
    }
}
