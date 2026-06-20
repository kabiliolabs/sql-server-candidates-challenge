
using Dapper;
using SyncTaskWorkerService.Data;
using SyncTaskWorkerService.Models;
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask.Handlers
{
    public class GetProductInventoryHandler(IDbConnection dbConnection) : ISyncTaskHandler
    {
        public string SyncName => "GetProductInventory";
        public async Task<IEnumerable<object>>HandleAsync(JsonElement parameters, CancellationToken ct)
        {
            DateTime modifiedSince = parameters.GetProperty("modifiedSince").GetDateTime();

            var connection = dbConnection.Create();

            var sql = @"
                SELECT 
                    pi.ProductID,
                    p.Name           AS ProductName,
                    p.ProductNumber,
                    l.Name           AS LocationName,
                    pi.Shelf,
                    pi.Bin,
                    pi.Quantity,
                    pi.ModifiedDate
                FROM Production.ProductInventory pi
                JOIN Production.Product p 
                    ON pi.ProductID = p.ProductID
                JOIN Production.Location l 
                    ON l.LocationID = pi.LocationID
                WHERE pi.ModifiedDate >= @ModifiedDate";

            var result = connection.Query(sql, new { ModifiedDate = modifiedSince });

            return result;
        }
    }
}
