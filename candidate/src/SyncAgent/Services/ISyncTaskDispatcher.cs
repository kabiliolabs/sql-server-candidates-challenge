using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Services;

public interface ISyncTaskDispatcher
{
    Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken);
}