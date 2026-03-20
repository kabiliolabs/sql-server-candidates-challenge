using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.DTOs;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Handlers;

public class GetProductInventoryHandler : ISyncTaskHandler
{
    public string TaskType => "GetProductInventory";

    private readonly string _connectionString;

    public GetProductInventoryHandler(IOptions<SyncAgentOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IEnumerable<object>> HandleAsync(
        TaskParameters parameters,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pi.ProductID,
                p.Name          AS ProductName,
                p.ProductNumber,
                l.Name          AS LocationName,
                pi.Shelf,
                pi.Bin,
                pi.Quantity,
                pi.ModifiedDate
            FROM Production.ProductInventory pi
            INNER JOIN Production.Product p
                ON pi.ProductID = p.ProductID
            INNER JOIN Production.Location l
                ON pi.LocationID = l.LocationID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
            """;

        await using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<ProductInventoryDto>(
            sql,
            new { parameters.ModifiedSince },
            commandTimeout: 60);

        return results.Cast<object>();
    }
}
