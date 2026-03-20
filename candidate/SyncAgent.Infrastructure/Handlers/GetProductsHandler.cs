using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.DTOs;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Handlers;

public class GetProductsHandler : ISyncTaskHandler
{
    public string TaskType => "GetProducts";

    private readonly string _connectionString;

    public GetProductsHandler(IOptions<SyncAgentOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IEnumerable<object>> HandleAsync(
        TaskParameters parameters,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.ProductID,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                pc.Name AS Category,
                ps.Name AS Subcategory,
                p.ModifiedDate
            FROM Production.Product p
            LEFT JOIN Production.ProductSubcategory ps
                ON p.ProductSubcategoryID = ps.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory pc
                ON ps.ProductCategoryID = pc.ProductCategoryID
            WHERE (@ModifiedSince IS NULL OR p.ModifiedDate >= @ModifiedSince)
            """;

        await using var connection = new SqlConnection(_connectionString);
        var results = await connection.QueryAsync<ProductDto>(
            sql,
            new { parameters.ModifiedSince },
            commandTimeout: 60);

        return results.Cast<object>();
    }
}
