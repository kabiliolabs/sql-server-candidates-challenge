using SyncAgent.Contracts;

namespace SyncAgent.Services;

public interface ISyncTaskExecutor
{
    Task<SyncResultContract> ExecuteAsync(SyncTaskContract task, CancellationToken cancellationToken);
}
