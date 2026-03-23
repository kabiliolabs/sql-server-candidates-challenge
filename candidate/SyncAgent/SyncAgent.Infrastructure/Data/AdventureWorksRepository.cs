using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Data;

public class AdventureWorksRepository : IAdventureWorksRepository
{
    private readonly string _connectionString;

    public AdventureWorksRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<CustomerDto>> GetCustomersAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                c.CustomerID      AS CustomerId,
                p.FirstName,
                p.LastName,
                p.EmailPromotion,
                ea.EmailAddress,
                a.AddressLine1,
                a.AddressLine2,
                a.City,
                sp.Name           AS StateProvince,
                cr.Name           AS CountryRegion,
                a.PostalCode,
                c.ModifiedDate
            FROM Sales.Customer c
            JOIN Person.Person              p   ON c.PersonID        = p.BusinessEntityID
            LEFT JOIN Person.EmailAddress   ea  ON p.BusinessEntityID = ea.BusinessEntityID
            LEFT JOIN (
                SELECT bea.BusinessEntityID, bea.AddressID,
                       ROW_NUMBER() OVER (PARTITION BY bea.BusinessEntityID ORDER BY bea.AddressID) AS rn
                FROM Person.BusinessEntityAddress bea
            ) ranked ON p.BusinessEntityID = ranked.BusinessEntityID AND ranked.rn = 1
            LEFT JOIN Person.Address        a   ON ranked.AddressID  = a.AddressID
            LEFT JOIN Person.StateProvince  sp  ON a.StateProvinceID = sp.StateProvinceID
            LEFT JOIN Person.CountryRegion  cr  ON sp.CountryRegionCode = cr.CountryRegionCode
            WHERE (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
            ORDER BY c.CustomerID
            """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<CustomerDto>(sql, new { ModifiedSince = modifiedSince });
    }

    public async Task<IEnumerable<ProductDto>> GetProductsAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                p.ProductID       AS ProductId,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                p.Size,
                p.Weight,
                pc.Name           AS Category,
                psc.Name          AS SubCategory,
                p.ProductLine,
                p.Style,
                p.SellStartDate,
                p.SellEndDate,
                p.DiscontinuedDate,
                p.ModifiedDate
            FROM Production.Product p
            LEFT JOIN Production.ProductSubcategory  psc ON p.ProductSubcategoryID = psc.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory     pc  ON psc.ProductCategoryID  = pc.ProductCategoryID
            WHERE (@ModifiedSince IS NULL OR p.ModifiedDate >= @ModifiedSince)
            ORDER BY p.ProductID
            """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<ProductDto>(sql, new { ModifiedSince = modifiedSince });
    }

    public async Task<IEnumerable<OrderDto>> GetOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                soh.SalesOrderID      AS SalesOrderId,
                soh.OrderDate,
                soh.DueDate,
                soh.ShipDate,
                soh.Status,
                soh.PurchaseOrderNumber,
                soh.AccountNumber,
                soh.CustomerID        AS CustomerId,
                p.FirstName           AS CustomerFirstName,
                p.LastName            AS CustomerLastName,
                soh.SubTotal,
                soh.TaxAmt,
                soh.Freight,
                soh.TotalDue,
                soh.ModifiedDate,
                sod.SalesOrderDetailID AS SalesOrderDetailId,
                sod.ProductID         AS ProductId,
                pr.Name               AS ProductName,
                sod.OrderQty,
                sod.UnitPrice,
                sod.UnitPriceDiscount,
                sod.LineTotal
            FROM Sales.SalesOrderHeader  soh
            JOIN Sales.SalesOrderDetail  sod ON soh.SalesOrderID = sod.SalesOrderID
            JOIN Production.Product      pr  ON sod.ProductID    = pr.ProductID
            JOIN Sales.Customer          c   ON soh.CustomerID   = c.CustomerID
            JOIN Person.Person           p   ON c.PersonID       = p.BusinessEntityID
            WHERE (@ModifiedSince IS NULL OR soh.ModifiedDate >= @ModifiedSince)
            ORDER BY soh.SalesOrderID, sod.SalesOrderDetailID
            """;

        using var conn = CreateConnection();

        // Use a dictionary to accumulate line items per order in a single pass
        var orderDict = new Dictionary<int, OrderDto>();

        await conn.QueryAsync<OrderDto, OrderLineItemDto, OrderDto>(
            sql,
            (order, lineItem) =>
            {
                if (!orderDict.TryGetValue(order.SalesOrderId, out var existing))
                {
                    existing = order with { LineItems = new List<OrderLineItemDto>() };
                    orderDict[order.SalesOrderId] = existing;
                }
                existing.LineItems.Add(lineItem);
                return existing;
            },
            new { ModifiedSince = modifiedSince },
            splitOn: "SalesOrderDetailId"
        );

        return orderDict.Values;
    }

    public async Task<IEnumerable<ProductInventoryDto>> GetProductInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                pi.ProductID      AS ProductId,
                p.Name            AS ProductName,
                pi.LocationID     AS LocationId,
                l.Name            AS LocationName,
                pi.Shelf,
                pi.Bin,
                pi.Quantity,
                pi.ModifiedDate
            FROM Production.ProductInventory pi
            JOIN Production.Product  p ON pi.ProductID  = p.ProductID
            JOIN Production.Location l ON pi.LocationID = l.LocationID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
            ORDER BY pi.ProductID, pi.LocationID
            """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<ProductInventoryDto>(sql, new { ModifiedSince = modifiedSince });
    }
}
