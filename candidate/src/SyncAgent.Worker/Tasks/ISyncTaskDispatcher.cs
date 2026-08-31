using SyncAgent.Worker.Platform;

namespace SyncAgent.Worker.Tasks;

public interface ISyncTaskDispatcher
{
    Task<object> DispatchAsync(SyncTask task, CancellationToken cancellationToken);
}
