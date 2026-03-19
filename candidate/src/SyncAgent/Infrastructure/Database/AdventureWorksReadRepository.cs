using Dapper;

namespace SyncAgent.Infrastructure.Database;

public sealed class AdventureWorksReadRepository(ISqlConnectionFactory connectionFactory) : IAdventureWorksReadRepository
{
    public async Task<IReadOnlyCollection<CustomerSyncDto>> GetCustomersAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH LatestEmail AS (
                SELECT
                    ea.BusinessEntityID,
                    ea.EmailAddress,
                    ea.ModifiedDate,
                    rn = ROW_NUMBER() OVER (PARTITION BY ea.BusinessEntityID ORDER BY ea.ModifiedDate DESC, ea.EmailAddressID DESC)
                FROM Person.EmailAddress ea
            ),
            LatestPhone AS (
                SELECT
                    pp.BusinessEntityID,
                    pp.PhoneNumber,
                    pp.ModifiedDate,
                    rn = ROW_NUMBER() OVER (PARTITION BY pp.BusinessEntityID ORDER BY pp.ModifiedDate DESC, pp.PhoneNumberTypeID ASC)
                FROM Person.PersonPhone pp
            ),
            LatestAddress AS (
                SELECT
                    bea.BusinessEntityID,
                    a.AddressLine1,
                    a.City,
                    a.PostalCode,
                    sp.Name AS StateProvince,
                    cr.Name AS CountryRegion,
                    a.ModifiedDate AS AddressModifiedDate,
                    sp.ModifiedDate AS StateProvinceModifiedDate,
                    cr.ModifiedDate AS CountryRegionModifiedDate,
                    rn = ROW_NUMBER() OVER (PARTITION BY bea.BusinessEntityID ORDER BY a.ModifiedDate DESC, bea.AddressTypeID ASC)
                FROM Person.BusinessEntityAddress bea
                INNER JOIN Person.Address a ON a.AddressID = bea.AddressID
                INNER JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
                INNER JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
            )
            SELECT
                c.CustomerID AS CustomerId,
                c.AccountNumber,
                p.FirstName,
                p.LastName,
                email.EmailAddress,
                phone.PhoneNumber AS Phone,
                address.AddressLine1,
                address.City,
                address.StateProvince,
                address.PostalCode,
                address.CountryRegion
            FROM Sales.Customer c
            INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN LatestEmail email ON email.BusinessEntityID = p.BusinessEntityID AND email.rn = 1
            LEFT JOIN LatestPhone phone ON phone.BusinessEntityID = p.BusinessEntityID AND phone.rn = 1
            LEFT JOIN LatestAddress address ON address.BusinessEntityID = p.BusinessEntityID AND address.rn = 1
            CROSS APPLY (
                SELECT MAX(v.ModifiedDate) AS MaxModifiedDate
                FROM (VALUES
                    (CAST(c.ModifiedDate AS datetime2)),
                    (CAST(p.ModifiedDate AS datetime2)),
                    (CAST(COALESCE(email.ModifiedDate, c.ModifiedDate) AS datetime2)),
                    (CAST(COALESCE(phone.ModifiedDate, c.ModifiedDate) AS datetime2)),
                    (CAST(COALESCE(address.AddressModifiedDate, c.ModifiedDate) AS datetime2)),
                    (CAST(COALESCE(address.StateProvinceModifiedDate, c.ModifiedDate) AS datetime2)),
                    (CAST(COALESCE(address.CountryRegionModifiedDate, c.ModifiedDate) AS datetime2))
                ) AS v(ModifiedDate)
            ) latest
            WHERE c.PersonID IS NOT NULL
              AND latest.MaxModifiedDate >= @ModifiedSinceUtc
            ORDER BY c.CustomerID;
            """;

        using var connection = connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { ModifiedSinceUtc = modifiedSince.UtcDateTime }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CustomerSyncDto>(command);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<ProductSyncDto>> GetProductsAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.ProductID AS ProductId,
                p.Name,
                p.ProductNumber,
                p.Color,
                p.StandardCost,
                p.ListPrice,
                category.Name AS Category,
                subcategory.Name AS Subcategory,
                p.ModifiedDate
            FROM Production.Product p
            LEFT JOIN Production.ProductSubcategory subcategory ON subcategory.ProductSubcategoryID = p.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory category ON category.ProductCategoryID = subcategory.ProductCategoryID
            WHERE p.ModifiedDate >= @ModifiedSinceUtc
            ORDER BY p.ProductID;
            """;

        using var connection = connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { ModifiedSinceUtc = modifiedSince.UtcDateTime }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ProductSyncDto>(command);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<ProductInventorySyncDto>> GetProductInventoryAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pi.ProductID AS ProductId,
                p.Name AS ProductName,
                p.ProductNumber,
                l.Name AS LocationName,
                pi.Shelf,
                pi.Bin,
                pi.Quantity,
                pi.ModifiedDate
            FROM Production.ProductInventory pi
            INNER JOIN Production.Product p ON p.ProductID = pi.ProductID
            INNER JOIN Production.Location l ON l.LocationID = pi.LocationID
            WHERE pi.ModifiedDate >= @ModifiedSinceUtc
            ORDER BY pi.ProductID, l.Name, pi.Shelf, pi.Bin;
            """;

        using var connection = connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { ModifiedSinceUtc = modifiedSince.UtcDateTime }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ProductInventorySyncDto>(command);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<OrderSyncDto>> GetOrdersAsync(DateTimeOffset modifiedSince, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                header.SalesOrderID,
                header.OrderDate,
                header.Status,
                customer.AccountNumber,
                CustomerName = COALESCE(
                    NULLIF(LTRIM(RTRIM(CONCAT(person.FirstName, ' ', person.LastName))), ''),
                    store.Name,
                    'Unknown Customer'),
                header.TotalDue,
                product.Name AS ProductName,
                product.ProductNumber,
                detail.UnitPrice,
                detail.OrderQty,
                detail.LineTotal
            FROM Sales.SalesOrderHeader header
            INNER JOIN Sales.Customer customer ON customer.CustomerID = header.CustomerID
            LEFT JOIN Person.Person person ON person.BusinessEntityID = customer.PersonID
            LEFT JOIN Sales.Store store ON store.BusinessEntityID = customer.StoreID
            INNER JOIN Sales.SalesOrderDetail detail ON detail.SalesOrderID = header.SalesOrderID
            INNER JOIN Production.Product product ON product.ProductID = detail.ProductID
            WHERE header.ModifiedDate >= @ModifiedSinceUtc
            ORDER BY header.SalesOrderID, detail.SalesOrderDetailID;
            """;

        using var connection = connectionFactory.CreateOpenConnection();
        var command = new CommandDefinition(sql, new { ModifiedSinceUtc = modifiedSince.UtcDateTime }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<OrderFlatRow>(command);

        var orders = rows
            .GroupBy(
                row => new
                {
                    row.SalesOrderId,
                    row.OrderDate,
                    row.Status,
                    row.CustomerName,
                    row.AccountNumber,
                    row.TotalDue
                })
            .Select(group => new OrderSyncDto
            {
                SalesOrderId = group.Key.SalesOrderId,
                OrderDate = DateTime.SpecifyKind(group.Key.OrderDate, DateTimeKind.Utc),
                Status = group.Key.Status,
                CustomerName = group.Key.CustomerName,
                AccountNumber = group.Key.AccountNumber,
                TotalDue = group.Key.TotalDue,
                OrderDetails = group.Select(detail => new OrderDetailSyncDto
                {
                    ProductName = detail.ProductName,
                    ProductNumber = detail.ProductNumber,
                    UnitPrice = detail.UnitPrice,
                    Quantity = detail.Quantity,
                    LineTotal = detail.LineTotal
                }).ToArray()
            })
            .OrderBy(order => order.SalesOrderId)
            .ToArray();

        return orders;
    }

    private sealed class OrderFlatRow
    {
        public int SalesOrderId { get; init; }
        public DateTime OrderDate { get; init; }
        public byte Status { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string AccountNumber { get; init; } = string.Empty;
        public decimal TotalDue { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string ProductNumber { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public short Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }
}
