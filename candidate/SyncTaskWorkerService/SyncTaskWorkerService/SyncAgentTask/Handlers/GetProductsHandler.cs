
using Dapper;
using SyncTaskWorkerService.Data;
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask.Handlers
{
    public class GetProductsHandler(IDbConnection dbConnection) : ISyncTaskHandler
    {
        public string SyncName => "GetProducts";
        public async Task<IEnumerable<object>> HandleAsync(JsonElement parameters, CancellationToken ct)
        {
            DateTime modifiedSince = parameters.GetProperty("modifiedSince").GetDateTime();

            var connection = dbConnection.Create();

            var sql = @"
                        SELECT 
                            p.ProductID,
                            p.Name,
                            p.ProductNumber,
                            p.Color,
                            p.StandardCost,
                            p.ListPrice,
                            pc.Name              AS Category,
                            psc.Name             AS Subcategory,
                            p.ModifiedDate
                        FROM Production.Product p
                        JOIN Production.ProductSubcategory psc 
                            ON p.ProductSubcategoryID = psc.ProductSubcategoryID
                        JOIN Production.ProductCategory pc 
                            ON psc.ProductCategoryID = pc.ProductCategoryID
                        WHERE p.ModifiedDate >= @ModifiedDate";

            var result = connection.Query(sql, new { ModifiedDate = modifiedSince });

            return result;
        }
    }
}
