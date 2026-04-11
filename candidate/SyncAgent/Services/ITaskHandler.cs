using SyncAgent.Models;

namespace SyncAgent.Services;

public interface ITaskHandler
{
    string TaskType { get; }
    Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default);
}
