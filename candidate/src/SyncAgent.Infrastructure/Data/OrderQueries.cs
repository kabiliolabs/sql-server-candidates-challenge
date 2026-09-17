namespace SyncAgent.Infrastructure.Data;

internal static class OrderQueries
{
    /// <summary>
    /// Two queries, assembled in memory (see OrderDetailQueries below) — never a single JOIN
    /// across header and detail, which would repeat the header once per order line and make
    /// TOP(@Top) cap order *lines* instead of orders.
    /// </summary>
    public const string SelectHeadersModifiedSince = """
        SELECT TOP (@Top)
            soh.SalesOrderID    AS SalesOrderId,
            soh.OrderDate,
            soh.Status,
            (p.FirstName + ' ' + p.LastName) AS CustomerName,
            soh.AccountNumber,
            soh.TotalDue
        FROM Sales.SalesOrderHeader soh
        INNER JOIN Sales.Customer c ON c.CustomerID = soh.CustomerID
        INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
        WHERE soh.ModifiedDate >= @ModifiedSince
        ORDER BY soh.SalesOrderID;
        """;

    public const string SelectDetailsForOrders = """
        SELECT
            sod.SalesOrderID   AS SalesOrderId,
            prod.Name           AS ProductName,
            prod.ProductNumber,
            sod.UnitPrice,
            sod.OrderQty        AS Quantity,
            sod.LineTotal
        FROM Sales.SalesOrderDetail sod
        INNER JOIN Production.Product prod ON prod.ProductID = sod.ProductID
        WHERE sod.SalesOrderID IN @SalesOrderIds
        ORDER BY sod.SalesOrderID, sod.SalesOrderDetailID;
        """;
}
