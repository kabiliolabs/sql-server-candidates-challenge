using Dapper;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Infrastructure.Data;

public sealed class ProductRepository(ISqlConnectionFactory connectionFactory, IOptions<SqlOptions> sqlOptions)
    : ISyncQueryRepository<ProductRecord>
{
    private readonly SqlOptions _sqlOptions = sqlOptions.Value;

    public async Task<IReadOnlyList<ProductRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.Create();

        var rows = await connection.QueryAsync<ProductRow>(
            new CommandDefinition(
                ProductQueries.SelectModifiedSince,
                new { ModifiedSince = modifiedSince.UtcDateTime, Top = maxRows },
                commandTimeout: _sqlOptions.CommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.Select(MapToRecord).ToList();
    }

    private static ProductRecord MapToRecord(ProductRow row) => new(
        row.ProductId,
        row.Name,
        row.ProductNumber,
        row.Color,
        row.StandardCost,
        row.ListPrice,
        row.Category,
        row.Subcategory,
        new DateTimeOffset(DateTime.SpecifyKind(row.ModifiedDate, DateTimeKind.Utc)));
}
