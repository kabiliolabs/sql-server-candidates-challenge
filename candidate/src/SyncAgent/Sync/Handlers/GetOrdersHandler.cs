using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;

namespace SyncAgent.Sync.Handlers;

/// <summary>
/// The one task type whose rows are not a straight projection: SQL returns one row per order line,
/// and the contract wants one record per order with its lines nested.
/// </summary>
/// <remarks>
/// Because <c>recordCount</c> is derived from the returned list, it reports orders rather than
/// detail lines -- which matches what <c>MaxRecords</c> caps. Posting 40,370 as the record count
/// for 12,304 orders would be a lie about what was synced.
/// </remarks>
internal sealed class GetOrdersHandler(ISqlQueryExecutor queryExecutor) : ISyncTaskHandler
{
    private readonly ISqlQueryExecutor _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));

    public string TaskType => "GetOrders";

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var rows = await _queryExecutor
            .QueryAsync<OrderRow>(SyncQueries.GetOrders, parameters.ToSqlParameters(), cancellationToken)
            .ConfigureAwait(false);

        return OrderResultBuilder.Build(rows);
    }
}
