using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;
using System.Data;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnection _db;
    private readonly IConfiguration _configuration;

    public ProductRepository(IConfiguration configuration)
    {
        _configuration = configuration;
        var connectionString = _configuration.GetConnectionString("MyConnection");
        _db = new SqlConnection(connectionString);
    }

    public async Task< IEnumerable<Product>> GetProductListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p.ProductID         as ProductId,
            	pc.Name             as Category,
                pps.Name            as Subcategory,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                p.ModifiedDate
            FROM Production.Product p
            JOIN Production.ProductSubcategory pps
                ON p.ProductSubcategoryID = pps.ProductSubcategoryID
            JOIN Production.ProductCategory pc
                ON pps.ProductCategoryID = pc.ProductCategoryID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)            
                   ORDER BY p.ProductID
            """;

        var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
        return await _db.QueryAsync<Product>(cmd);
    }
}