namespace SyncAgent.Infrastructure.Data;

internal static class ProductQueries
{
    public const string SelectModifiedSince = """
        SELECT TOP (@Top)
            p.ProductID     AS ProductId,
            p.Name,
            p.ProductNumber,
            p.Color,
            p.StandardCost,
            p.ListPrice,
            cat.Name         AS Category,
            sc.Name          AS Subcategory,
            p.ModifiedDate
        FROM Production.Product p
        LEFT JOIN Production.ProductSubcategory sc ON sc.ProductSubcategoryID = p.ProductSubcategoryID
        LEFT JOIN Production.ProductCategory cat ON cat.ProductCategoryID = sc.ProductCategoryID
        WHERE p.ModifiedDate >= @ModifiedSince
        ORDER BY p.ProductID;
        """;
}
