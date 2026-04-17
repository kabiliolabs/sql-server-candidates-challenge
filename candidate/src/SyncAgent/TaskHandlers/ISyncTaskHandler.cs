using SyncAgent.Contracts;
using SyncAgent.Domain;

namespace SyncAgent.TaskHandlers;

public interface ISyncTaskHandler
{
    string TaskType { get; }
    Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken);
}
