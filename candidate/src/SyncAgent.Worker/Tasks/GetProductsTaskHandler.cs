using Dapper;
using SyncAgent.Worker.Data;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks.Models;

namespace SyncAgent.Worker.Tasks;

public class GetProductsTaskHandler(ISqlConnectionFactory connectionFactory) : ISyncTaskHandler
{
    public SyncTaskType TaskType => SyncTaskType.GetProducts;

    private const string Sql = """
        SELECT
            p.ProductID AS ProductId,
            p.Name,
            p.ProductNumber,
            p.Color,
            p.StandardCost,
            p.ListPrice,
            pc.Name AS Category,
            psc.Name AS Subcategory,
            p.ModifiedDate
        FROM Production.Product p
        LEFT JOIN Production.ProductSubcategory psc ON psc.ProductSubcategoryID = p.ProductSubcategoryID
        LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = psc.ProductCategoryID
        WHERE (@ModifiedSince IS NULL OR p.ModifiedDate >= @ModifiedSince)
        """;

    public async Task<object> ExecuteAsync(TaskParameters? parameters, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            Sql,
            new { parameters?.ModifiedSince },
            cancellationToken: cancellationToken);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await connection.QueryAsync<ProductResult>(command);
        return results.AsList();
    }
}
