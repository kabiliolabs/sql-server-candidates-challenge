using Dapper;
using TaskExecutionService.Data;
using TaskExecutionService.Models;

namespace TaskExecutionService.Sync.Executors;

/// <summary>Returns products (with category/subcategory names) modified since the given date.</summary>
public sealed class GetProductsExecutor : ISyncTaskExecutor
{
    public const string Type = "GetProducts";

    private const string Sql = """
        SELECT
            p.ProductID      AS ProductId,
            p.Name           AS Name,
            p.ProductNumber  AS ProductNumber,
            p.Color          AS Color,
            p.StandardCost   AS StandardCost,
            p.ListPrice      AS ListPrice,
            pc.Name          AS Category,
            ps.Name          AS Subcategory,
            p.ModifiedDate   AS ModifiedDate
        FROM Production.Product p
        LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
        LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID
        WHERE p.ModifiedDate >= @modifiedSince
        ORDER BY p.ProductID;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public GetProductsExecutor(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string TaskType => Type;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            Sql,
            new { modifiedSince = parameters.ModifiedSince },
            commandTimeout: _connectionFactory.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<ProductDto>(command);
        return rows.ToArray();
    }
}
