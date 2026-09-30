using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Handlers.Products
{
    public sealed class GetProductsHandler(
        IDbConnectionFactory connectionFactory,
        IOptions<SyncOptions> options,
        ILogger<GetProductsHandler> logger) : SqlQueryHandler<ProductRecord>(connectionFactory, options, logger)
    {
        public override string TaskType => SyncTaskTypes.GetProducts;

        protected override string Sql => """
            SELECT TOP (@MaxRecords)
                pr.ProductID     AS ProductId,
                pr.Name          AS Name,
                pr.ProductNumber AS ProductNumber,
                pr.Color         AS Color,
                pr.StandardCost  AS StandardCost,
                pr.ListPrice     AS ListPrice,
                pc.Name          AS Category,
                ps.Name          AS Subcategory,
                pr.ModifiedDate  AS ModifiedDate
            FROM Production.Product pr
            LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = pr.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory    pc ON pc.ProductCategoryID    = ps.ProductCategoryID
            WHERE pr.ModifiedDate >= @ModifiedSince
            ORDER BY pr.ProductID;
            """;
    }
}