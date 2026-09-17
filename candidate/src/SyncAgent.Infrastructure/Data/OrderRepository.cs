using Dapper;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Infrastructure.Data;

/// <summary>Row shape returned by the header query — everything OrderRecord needs except the
/// nested OrderDetails, which come from a second query and get assembled in memory. OrderDate
/// is DateTime (matching the SQL `datetime` column), not DateTimeOffset — see ProductRow for
/// why Dapper needs that distinction; the conversion happens explicitly below.</summary>
internal sealed record OrderHeaderRow(
    int SalesOrderId,
    DateTime OrderDate,
    byte Status,
    string? CustomerName,
    string? AccountNumber,
    decimal TotalDue);

internal sealed record OrderDetailRow(
    int SalesOrderId,
    string ProductName,
    string ProductNumber,
    decimal UnitPrice,
    short Quantity,
    decimal LineTotal);

public sealed class OrderRepository(ISqlConnectionFactory connectionFactory, IOptions<SqlOptions> sqlOptions)
    : ISyncQueryRepository<OrderRecord>
{
    // SQL Server caps a single parameterized query at 2100 parameters, and Dapper expands
    // `IN @Ids` into one parameter per id — with MaxRowsPerTask defaulting to 5000, a single
    // IN clause over every order id would blow past that limit. Batching keeps each detail
    // query well under it regardless of how many orders the header query returns.
    private const int DetailQueryBatchSize = 2000;

    private readonly SqlOptions _sqlOptions = sqlOptions.Value;

    public async Task<IReadOnlyList<OrderRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.Create();

        var headers = (await connection.QueryAsync<OrderHeaderRow>(
            new CommandDefinition(
                OrderQueries.SelectHeadersModifiedSince,
                new { ModifiedSince = modifiedSince.UtcDateTime, Top = maxRows },
                commandTimeout: _sqlOptions.CommandTimeoutSeconds,
                cancellationToken: cancellationToken))).ToList();

        if (headers.Count == 0)
        {
            return [];
        }

        var orderIds = headers.Select(h => h.SalesOrderId).ToList();
        var details = new List<OrderDetailRow>(orderIds.Count);

        foreach (var batch in orderIds.Chunk(DetailQueryBatchSize))
        {
            var batchDetails = await connection.QueryAsync<OrderDetailRow>(
                new CommandDefinition(
                    OrderQueries.SelectDetailsForOrders,
                    new { SalesOrderIds = batch },
                    commandTimeout: _sqlOptions.CommandTimeoutSeconds,
                    cancellationToken: cancellationToken));
            details.AddRange(batchDetails);
        }

        var detailsByOrderId = details
            .GroupBy(d => d.SalesOrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderDetailRecord>)g
                .Select(d => new OrderDetailRecord(d.ProductName, d.ProductNumber, d.UnitPrice, d.Quantity, d.LineTotal))
                .ToList());

        return headers
            .Select(h => new OrderRecord(
                h.SalesOrderId,
                new DateTimeOffset(DateTime.SpecifyKind(h.OrderDate, DateTimeKind.Utc)),
                h.Status,
                h.CustomerName,
                h.AccountNumber,
                h.TotalDue,
                detailsByOrderId.GetValueOrDefault(h.SalesOrderId, [])))
            .ToList();
    }
}
