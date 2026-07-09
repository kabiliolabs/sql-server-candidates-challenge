namespace SyncAgent.Data.Queries;

/// <summary>
/// The four sync queries, as constants.
/// </summary>
/// <remarks>
/// <para>
/// Every statement is a compile-time constant. Nothing is composed, formatted or interpolated at
/// run time, so there is no code path by which an externally-supplied value could become SQL.
/// <c>@ModifiedSince</c> and <c>@MaxRecords</c> are always bound as parameters.
/// </para>
/// <para>
/// Column aliases match the DTO property names exactly, because that is what the mapper binds on.
/// tinyint and smallint columns are cast to int in SQL rather than left to an implicit numeric
/// conversion in the mapper.
/// </para>
/// </remarks>
internal static class SyncQueries
{
    /// <summary>
    /// 24 customers have more than one address, so a plain join to Person.BusinessEntityAddress
    /// would duplicate those customer rows. OUTER APPLY collapses the fan-out to exactly one
    /// address -- preferring Home (AddressTypeID 2), then the lowest AddressID for determinism.
    /// Email and phone are 1:1 per customer (verified: no customer has two), so a LEFT JOIN is
    /// safe for those. LEFT, not INNER, throughout: 701 customers are stores with no person.
    /// </summary>
    public const string GetCustomers = """
        SELECT c.CustomerID       AS CustomerId,
               c.AccountNumber    AS AccountNumber,
               p.FirstName        AS FirstName,
               p.LastName         AS LastName,
               e.EmailAddress     AS EmailAddress,
               pp.PhoneNumber     AS Phone,
               addr.AddressLine1  AS AddressLine1,
               addr.City          AS City,
               addr.StateProvince AS StateProvince,
               addr.PostalCode    AS PostalCode,
               addr.CountryRegion AS CountryRegion
        FROM Sales.Customer c
        LEFT JOIN Person.Person       p  ON p.BusinessEntityID  = c.PersonID
        LEFT JOIN Person.EmailAddress e  ON e.BusinessEntityID  = c.PersonID
        LEFT JOIN Person.PersonPhone  pp ON pp.BusinessEntityID = c.PersonID
        OUTER APPLY (
            SELECT TOP (1) a.AddressLine1,
                           a.City,
                           sp.Name AS StateProvince,
                           a.PostalCode,
                           cr.Name AS CountryRegion
            FROM Person.BusinessEntityAddress bea
            JOIN Person.Address       a  ON a.AddressID          = bea.AddressID
            JOIN Person.StateProvince sp ON sp.StateProvinceID   = a.StateProvinceID
            JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
            WHERE bea.BusinessEntityID = c.PersonID
            ORDER BY CASE bea.AddressTypeID WHEN 2 THEN 0 ELSE 1 END, bea.AddressID
        ) addr
        WHERE c.ModifiedDate >= @ModifiedSince
        ORDER BY c.CustomerID
        OFFSET 0 ROWS FETCH NEXT @MaxRecords ROWS ONLY;
        """;

    /// <summary>
    /// LEFT JOIN to subcategory and category: 209 of 504 products have no ProductSubcategoryID,
    /// and an INNER JOIN would drop 41% of the catalogue without any error to show for it.
    /// </summary>
    public const string GetProducts = """
        SELECT p.ProductID     AS ProductId,
               p.Name          AS Name,
               p.ProductNumber AS ProductNumber,
               p.Color         AS Color,
               p.StandardCost  AS StandardCost,
               p.ListPrice     AS ListPrice,
               pc.Name         AS Category,
               ps.Name         AS Subcategory,
               p.ModifiedDate  AS ModifiedDate
        FROM Production.Product p
        LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID
        LEFT JOIN Production.ProductCategory    pc ON pc.ProductCategoryID    = ps.ProductCategoryID
        WHERE p.ModifiedDate >= @ModifiedSince
        ORDER BY p.ProductID
        OFFSET 0 ROWS FETCH NEXT @MaxRecords ROWS ONLY;
        """;

    /// <summary>
    /// The cap counts orders, not line items. A CTE selects the capped set of headers first, and
    /// only then joins the details, so @MaxRecords can never slice an order in half and post a
    /// partial basket. 12,304 orders pass the filter and expand to 40,370 detail lines, which is
    /// why the cap exists at all.
    /// <para>
    /// COALESCE(person name, store name): sample order 43659 belongs to a store, yet its
    /// customerName is the store's contact person. Person name wins where there is one.
    /// </para>
    /// </summary>
    public const string GetOrders = """
        WITH h AS (
            SELECT TOP (@MaxRecords)
                   soh.SalesOrderID,
                   soh.OrderDate,
                   soh.Status,
                   soh.TotalDue,
                   soh.CustomerID
            FROM Sales.SalesOrderHeader soh
            WHERE soh.ModifiedDate >= @ModifiedSince
            ORDER BY soh.SalesOrderID
        )
        SELECT h.SalesOrderID                                AS SalesOrderId,
               h.OrderDate                                   AS OrderDate,
               CAST(h.Status AS int)                         AS Status,
               COALESCE(p.FirstName + ' ' + p.LastName, s.Name) AS CustomerName,
               c.AccountNumber                               AS AccountNumber,
               h.TotalDue                                    AS TotalDue,
               pr.Name                                       AS ProductName,
               pr.ProductNumber                              AS ProductNumber,
               sod.UnitPrice                                 AS UnitPrice,
               CAST(sod.OrderQty AS int)                     AS Quantity,
               sod.LineTotal                                 AS LineTotal
        FROM h
        JOIN      Sales.Customer         c   ON c.CustomerID       = h.CustomerID
        LEFT JOIN Person.Person          p   ON p.BusinessEntityID = c.PersonID
        LEFT JOIN Sales.Store            s   ON s.BusinessEntityID = c.StoreID
        JOIN      Sales.SalesOrderDetail sod ON sod.SalesOrderID   = h.SalesOrderID
        JOIN      Production.Product     pr  ON pr.ProductID       = sod.ProductID
        ORDER BY h.SalesOrderID, sod.SalesOrderDetailID;
        """;

    /// <summary>
    /// Multiple rows per product is correct here: the primary key is (ProductID, LocationID).
    /// Shelf is a padded nvarchar in places, so it is trimmed at the source.
    /// </summary>
    public const string GetProductInventory = """
        SELECT pi.ProductID             AS ProductId,
               p.Name                   AS ProductName,
               p.ProductNumber          AS ProductNumber,
               l.Name                   AS LocationName,
               LTRIM(RTRIM(pi.Shelf))   AS Shelf,
               CAST(pi.Bin AS int)      AS Bin,
               CAST(pi.Quantity AS int) AS Quantity,
               pi.ModifiedDate          AS ModifiedDate
        FROM Production.ProductInventory pi
        JOIN Production.Product  p ON p.ProductID  = pi.ProductID
        JOIN Production.Location l ON l.LocationID = pi.LocationID
        WHERE pi.ModifiedDate >= @ModifiedSince
        ORDER BY pi.ProductID, l.LocationID
        OFFSET 0 ROWS FETCH NEXT @MaxRecords ROWS ONLY;
        """;
}
