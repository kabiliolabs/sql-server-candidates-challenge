using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;

namespace SyncAgent.Sync.Handlers;

internal sealed class GetProductInventoryHandler(ISqlQueryExecutor queryExecutor)
    : ProjectionSyncTaskHandler<InventoryDto>(queryExecutor)
{
    public override string TaskType => "GetProductInventory";

    protected override string Sql => SyncQueries.GetProductInventory;
}
