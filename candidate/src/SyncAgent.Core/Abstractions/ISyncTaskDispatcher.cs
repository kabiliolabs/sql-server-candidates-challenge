using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    public interface ISyncTaskDispatcher
    {
        Task<SyncResult> DispatchAsync(SyncTask task, CancellationToken cancellationToken);
    }
}