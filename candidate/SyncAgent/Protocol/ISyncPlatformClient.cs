using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Domain;

namespace SyncAgent.Protocol
{
    /// <summary>Gateway to the central platform: pick up the next task, report results.
    /// 204 means "no work", never an error.</summary>
    public interface ISyncPlatformClient
    {
        Task<SyncTask> GetNextTaskAsync(CancellationToken cancellationToken);
        Task<bool> PostResultAsync(SyncResult result, CancellationToken cancellationToken);
    }
}