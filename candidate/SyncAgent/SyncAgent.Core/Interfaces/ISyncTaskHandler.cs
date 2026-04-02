using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

public interface ISyncTaskHandler
{
    string TaskType { get; }
    Task<SyncResult> HandleAsync(SyncTask task, CancellationToken ct = default);
}
