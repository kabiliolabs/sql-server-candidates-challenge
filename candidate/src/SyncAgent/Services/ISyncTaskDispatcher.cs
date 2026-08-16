using SyncAgent.Models;

namespace SyncAgent.Services;

public interface ISyncTaskDispatcher
{
    Task<SyncTaskExecution> ExecuteAsync(SyncTask task, CancellationToken cancellationToken);
}