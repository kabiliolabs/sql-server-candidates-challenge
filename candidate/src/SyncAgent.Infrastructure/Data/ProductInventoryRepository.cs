using Dapper;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Infrastructure.Data;

public sealed class ProductInventoryRepository(ISqlConnectionFactory connectionFactory, IOptions<SqlOptions> sqlOptions)
    : ISyncQueryRepository<ProductInventoryRecord>
{
    private readonly SqlOptions _sqlOptions = sqlOptions.Value;

    public async Task<IReadOnlyList<ProductInventoryRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ProductInventoryRow>(
            new CommandDefinition(
                ProductInventoryQueries.SelectModifiedSince,
                new { ModifiedSince = modifiedSince.UtcDateTime, Top = maxRows },
                commandTimeout: _sqlOptions.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.Select(MapToRecord).ToList();
    }

    private static ProductInventoryRecord MapToRecord(ProductInventoryRow row) => new(
        row.ProductId,
        row.ProductName,
        row.ProductNumber,
        row.LocationName,
        row.Shelf,
        row.Bin,
        row.Quantity,
        new DateTimeOffset(DateTime.SpecifyKind(row.ModifiedDate, DateTimeKind.Utc)));
}
