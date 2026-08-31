using Dapper;
using SyncAgent.Worker.Data;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks.Models;

namespace SyncAgent.Worker.Tasks;

public class GetProductInventoryTaskHandler(ISqlConnectionFactory connectionFactory) : ISyncTaskHandler
{
    public SyncTaskType TaskType => SyncTaskType.GetProductInventory;

    // One row per (product, location) - a product stocked in multiple locations
    // legitimately yields multiple rows here, matching how ProductInventory itself
    // is modeled (not a bug/fan-out to guard against, unlike GetCustomers).
    private const string Sql = """
        SELECT
            p.ProductID AS ProductId,
            p.Name AS ProductName,
            p.ProductNumber,
            l.Name AS LocationName,
            pi.Shelf,
            pi.Bin,
            pi.Quantity,
            pi.ModifiedDate
        FROM Production.ProductInventory pi
        INNER JOIN Production.Product p ON p.ProductID = pi.ProductID
        INNER JOIN Production.Location l ON l.LocationID = pi.LocationID
        WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
        """;

    public async Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            Sql,
            new { parameters?.ModifiedSince },
            cancellationToken: cancellationToken);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await connection.QueryAsync<ProductInventoryResult>(command);
        return results.AsList();
    }
}
