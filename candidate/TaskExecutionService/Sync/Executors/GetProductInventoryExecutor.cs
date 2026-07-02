using Dapper;
using TaskExecutionService.Data;
using TaskExecutionService.Models;

namespace TaskExecutionService.Sync.Executors;

/// <summary>Returns product inventory rows (one per product per location) modified since the given date.</summary>
public sealed class GetProductInventoryExecutor : ISyncTaskExecutor
{
    public const string Type = "GetProductInventory";

    private const string Sql = """
        SELECT
            pi.ProductID    AS ProductId,
            p.Name          AS ProductName,
            p.ProductNumber AS ProductNumber,
            l.Name          AS LocationName,
            pi.Shelf        AS Shelf,
            pi.Bin          AS Bin,
            pi.Quantity     AS Quantity,
            pi.ModifiedDate AS ModifiedDate
        FROM Production.ProductInventory pi
        INNER JOIN Production.Product p ON p.ProductID = pi.ProductID
        INNER JOIN Production.Location l ON l.LocationID = pi.LocationID
        WHERE pi.ModifiedDate >= @modifiedSince
        ORDER BY pi.ProductID, l.Name;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public GetProductInventoryExecutor(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string TaskType => Type;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            Sql,
            new { modifiedSince = parameters.ModifiedSince },
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<ProductInventoryDto>(command);
        return rows.ToArray();
    }
}
