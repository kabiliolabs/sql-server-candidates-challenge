using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Domain.Models;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Sql;

public sealed class InventoryRepository(IOptions<DatabaseOptions> options, ILogger<InventoryRepository> logger)
    : IInventoryRepository
{
    // ProductInventory is keyed by product and location, so a product held in several locations
    // is reported once per location.
    private const string Sql = """
        SELECT
            pi.ProductID,
            p.Name AS ProductName,
            p.ProductNumber,
            l.Name AS LocationName,
            pi.Shelf,
            pi.Bin,
            pi.Quantity,
            pi.ModifiedDate
        FROM Production.ProductInventory pi
        INNER JOIN Production.Product p ON pi.ProductID = p.ProductID
        INNER JOIN Production.Location l ON pi.LocationID = l.LocationID
        WHERE pi.ModifiedDate >= @ModifiedSince
        ORDER BY pi.ProductID, pi.LocationID
        """;

    private readonly DatabaseOptions _options = options.Value;

    public async Task<List<InventoryItem>> GetInventoryAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var items = new List<InventoryItem>();

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        command.Parameters.Add("@ModifiedSince", SqlDbType.DateTime).Value = modifiedSince;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new InventoryItem
            {
                ProductId = reader.GetInt32(0),
                ProductName = reader.GetString(1),
                ProductNumber = reader.GetString(2),
                LocationName = reader.GetString(3),
                Shelf = reader.GetString(4),
                Bin = reader.GetByte(5),
                Quantity = reader.GetInt16(6),
                ModifiedDate = reader.GetUtcDateTime(7)
            });
        }

        logger.LogInformation("Retrieved {InventoryItemCount} inventory items", items.Count);
        return items;
    }
}
