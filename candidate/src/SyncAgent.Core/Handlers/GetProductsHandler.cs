using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Handlers;

public sealed class GetProductsHandler(ISyncQueryRepository<ProductRecord> repository, IOptions<SyncAgentOptions> options) : ISyncTaskHandler
{
    public string TaskType => TaskTypes.GetProducts;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskRequest request, CancellationToken cancellationToken)
    {
        var records = await repository.GetModifiedSinceAsync(request.ModifiedSince, options.Value.MaxRowsPerTask, cancellationToken);
        return records.Cast<object>().ToList();
    }
}
