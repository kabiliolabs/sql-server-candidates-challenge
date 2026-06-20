
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask
{
    public interface ISyncTaskHandler
    {
        string SyncName { get; }
        Task<IEnumerable<object>> HandleAsync(JsonElement parameters, CancellationToken ct);
    }
}
