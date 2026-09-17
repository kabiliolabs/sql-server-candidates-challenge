namespace SyncAgent.Infrastructure.Data;

internal static class ProductInventoryQueries
{
    /// <summary>
    /// Filters on ProductInventory.ModifiedDate — NOT Product.ModifiedDate, which is a
    /// different column that happens to also look plausible but gives the wrong row count
    /// (1,069 vs. the correct 609 for the >= 2025-01-01 filter used by the platform).
    /// </summary>
    public const string SelectModifiedSince = """
        SELECT TOP (@Top)
            pi.ProductID     AS ProductId,
            p.Name            AS ProductName,
            p.ProductNumber,
            l.Name            AS LocationName,
            pi.Shelf,
            pi.Bin,
            pi.Quantity,
            pi.ModifiedDate
        FROM Production.ProductInventory pi
        INNER JOIN Production.Product p ON p.ProductID = pi.ProductID
        LEFT JOIN Production.Location l ON l.LocationID = pi.LocationID
        WHERE pi.ModifiedDate >= @ModifiedSince
        ORDER BY pi.ProductID, pi.LocationID;
        """;
}
