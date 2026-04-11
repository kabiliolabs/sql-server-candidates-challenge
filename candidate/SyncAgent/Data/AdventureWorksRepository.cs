using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SyncAgent.Data.Dto;

namespace SyncAgent.Data;

public class AdventureWorksRepository : IAdventureWorksRepository
{
    private readonly string _connectionString;

    public AdventureWorksRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("AdventureWorks")
            ?? throw new InvalidOperationException("Connection string 'AdventureWorks' is not configured.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<IEnumerable<CustomerDto>> GetCustomersAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.CustomerID        AS CustomerId,
                c.AccountNumber,
                p.FirstName,
                p.LastName,
                ea.EmailAddress,
                ph.PhoneNumber      AS Phone,
                a.AddressLine1,
                a.City,
                sp.Name             AS StateProvince,
                a.PostalCode,
                cr.Name             AS CountryRegion
            FROM Sales.Customer c
            JOIN Person.Person p
                ON c.PersonID = p.BusinessEntityID
            LEFT JOIN Person.EmailAddress ea
                ON p.BusinessEntityID = ea.BusinessEntityID
            LEFT JOIN Person.PersonPhone ph
                ON p.BusinessEntityID = ph.BusinessEntityID
                AND ph.PhoneNumberTypeID = 1
            LEFT JOIN Person.BusinessEntityAddress bea
                ON p.BusinessEntityID = bea.BusinessEntityID
                AND bea.AddressTypeID = 2
            LEFT JOIN Person.Address a
                ON bea.AddressID = a.AddressID
            LEFT JOIN Person.StateProvince sp
                ON a.StateProvinceID = sp.StateProvinceID
            LEFT JOIN Person.CountryRegion cr
                ON sp.CountryRegionCode = cr.CountryRegionCode
            WHERE (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
            ORDER BY c.CustomerID
            """;

        using var conn = CreateConnection();
        var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
        return await conn.QueryAsync<CustomerDto>(cmd);
    }

    public async Task<IEnumerable<ProductDto>> GetProductsAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                p.ProductID         AS ProductId,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                pc.Name             AS Category,
                ps.Name             AS Subcategory,
                p.ModifiedDate
            FROM Production.Product p
            LEFT JOIN Production.ProductSubcategory ps
                ON p.ProductSubcategoryID = ps.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory pc
                ON ps.ProductCategoryID = pc.ProductCategoryID
            WHERE (@ModifiedSince IS NULL OR p.ModifiedDate >= @ModifiedSince)
            ORDER BY p.ProductID
            """;

        using var conn = CreateConnection();
        var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
        return await conn.QueryAsync<ProductDto>(cmd);
    }

    public async Task<IEnumerable<OrderFlatRow>> GetOrderFlatRowsAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                soh.SalesOrderID                            AS SalesOrderId,
                soh.OrderDate,
                soh.Status,
                p.FirstName + ' ' + p.LastName              AS CustomerName,
                c.AccountNumber,
                soh.TotalDue,
                pr.Name                                     AS ProductName,
                pr.ProductNumber,
                sod.UnitPrice,
                sod.OrderQty                                AS Quantity,
                sod.LineTotal
            FROM Sales.SalesOrderHeader soh
            JOIN Sales.Customer c
                ON soh.CustomerID = c.CustomerID
            JOIN Person.Person p
                ON c.PersonID = p.BusinessEntityID
            JOIN Sales.SalesOrderDetail sod
                ON soh.SalesOrderID = sod.SalesOrderID
            JOIN Production.Product pr
                ON sod.ProductID = pr.ProductID
            WHERE (@ModifiedSince IS NULL OR soh.ModifiedDate >= @ModifiedSince)
            ORDER BY soh.SalesOrderID
            """;

        using var conn = CreateConnection();
        var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
        return await conn.QueryAsync<OrderFlatRow>(cmd);
    }

    public async Task<IEnumerable<ProductInventoryDto>> GetProductInventoryAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                pi.ProductID        AS ProductId,
                pr.Name             AS ProductName,
                pr.ProductNumber,
                l.Name              AS LocationName,
                pi.Shelf,
                pi.Bin,
                pi.Quantity,
                pi.ModifiedDate
            FROM Production.ProductInventory pi
            JOIN Production.Product pr
                ON pi.ProductID = pr.ProductID
            JOIN Production.Location l
                ON pi.LocationID = l.LocationID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
            ORDER BY pi.ProductID, l.LocationID
            """;

        using var conn = CreateConnection();
        var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
        return await conn.QueryAsync<ProductInventoryDto>(cmd);
    }
}
