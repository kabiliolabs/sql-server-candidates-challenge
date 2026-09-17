using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Handlers;

public sealed class GetOrdersHandler(ISyncQueryRepository<OrderRecord> repository, IOptions<SyncAgentOptions> options) : ISyncTaskHandler
{
    public string TaskType => TaskTypes.GetOrders;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskRequest request, CancellationToken cancellationToken)
    {
        var records = await repository.GetModifiedSinceAsync(request.ModifiedSince, options.Value.MaxRowsPerTask, cancellationToken);
        return records.Cast<object>().ToList();
    }
}
