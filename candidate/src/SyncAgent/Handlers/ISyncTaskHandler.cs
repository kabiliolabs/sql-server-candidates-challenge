using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Handlers;

public interface ISyncTaskHandler
{
    string TaskType { get; }

    Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken);
}