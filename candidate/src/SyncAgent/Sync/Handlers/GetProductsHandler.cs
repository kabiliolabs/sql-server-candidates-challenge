using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;

namespace SyncAgent.Sync.Handlers;

internal sealed class GetProductsHandler(ISqlQueryExecutor queryExecutor)
    : ProjectionSyncTaskHandler<ProductDto>(queryExecutor)
{
    public override string TaskType => "GetProducts";

    protected override string Sql => SyncQueries.GetProducts;
}
