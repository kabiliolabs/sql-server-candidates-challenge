using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;

namespace SyncAgent.Sync.Handlers;

internal sealed class GetCustomersHandler(ISqlQueryExecutor queryExecutor)
    : ProjectionSyncTaskHandler<CustomerDto>(queryExecutor)
{
    public override string TaskType => "GetCustomers";

    protected override string Sql => SyncQueries.GetCustomers;
}
