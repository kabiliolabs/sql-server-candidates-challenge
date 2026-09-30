using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    public interface ISyncTaskHandler
    {
        string TaskType { get; }

        Task<IReadOnlyList<object>> ExecuteAsync(SyncQuery query, CancellationToken cancellationToken);
    }
}