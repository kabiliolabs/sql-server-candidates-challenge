using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Domain;

namespace SyncAgent.Protocol
{
    /// <summary>One implementation per task type; the dispatcher selects by TaskType.</summary>
    public interface ISyncTaskHandler
    {
        string TaskType { get; }
        Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken);
    }
}