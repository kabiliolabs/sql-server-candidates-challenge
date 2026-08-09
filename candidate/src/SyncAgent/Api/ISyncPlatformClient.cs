using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Api;

public interface ISyncPlatformClient
{
    Task<SyncTask?> GetNextTaskAsync(
        CancellationToken cancellationToken);

    Task SubmitResultAsync(
        SyncResult result,
        CancellationToken cancellationToken);
}