using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Domain.Models;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Sql;

public sealed class ProductRepository(IOptions<DatabaseOptions> options, ILogger<ProductRepository> logger)
    : IProductRepository
{
    // The subcategory joins are LEFT because ProductSubcategoryID is nullable: raw materials and
    // similar products have no category and must still be reported.
    private const string Sql = """
        SELECT
            p.ProductID,
            p.Name,
            p.ProductNumber,
            p.Color,
            p.StandardCost,
            p.ListPrice,
            pc.Name AS Category,
            psc.Name AS Subcategory,
            p.ModifiedDate
        FROM Production.Product p
        LEFT JOIN Production.ProductSubcategory psc ON p.ProductSubcategoryID = psc.ProductSubcategoryID
        LEFT JOIN Production.ProductCategory pc ON psc.ProductCategoryID = pc.ProductCategoryID
        WHERE p.ModifiedDate >= @ModifiedSince
        ORDER BY p.ProductID
        """;

    private readonly DatabaseOptions _options = options.Value;

    public async Task<List<Product>> GetProductsAsync(DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var products = new List<Product>();

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Sql, connection) { CommandTimeout = _options.CommandTimeoutSeconds };
        command.Parameters.Add("@ModifiedSince", SqlDbType.DateTime).Value = modifiedSince;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new Product
            {
                ProductId = reader.GetInt32(0),
                Name = reader.GetString(1),
                ProductNumber = reader.GetString(2),
                Color = reader.IsDBNull(3) ? null : reader.GetString(3),
                StandardCost = reader.GetDecimal(4),
                ListPrice = reader.GetDecimal(5),
                Category = reader.IsDBNull(6) ? null : reader.GetString(6),
                Subcategory = reader.IsDBNull(7) ? null : reader.GetString(7),
                ModifiedDate = reader.GetUtcDateTime(8)
            });
        }

        logger.LogInformation("Retrieved {ProductCount} products", products.Count);
        return products;
    }
}
