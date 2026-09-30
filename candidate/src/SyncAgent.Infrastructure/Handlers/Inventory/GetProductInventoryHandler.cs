using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Handlers.Inventory
{
    public sealed class GetProductInventoryHandler(
        IDbConnectionFactory connectionFactory,
        IOptions<SyncOptions> options,
        ILogger<GetProductInventoryHandler> logger) : SqlQueryHandler<ProductInventoryRecord>(connectionFactory, options, logger)
    {
        public override string TaskType => SyncTaskTypes.GetProductInventory;

        protected override string Sql => """
            SELECT TOP (@MaxRecords)
                pr.ProductID     AS ProductId,
                pr.Name          AS ProductName,
                pr.ProductNumber AS ProductNumber,
                l.Name           AS LocationName,
                pi.Shelf         AS Shelf,
                pi.Bin           AS Bin,
                pi.Quantity      AS Quantity,
                pi.ModifiedDate  AS ModifiedDate
            FROM Production.ProductInventory pi
            JOIN Production.Product  pr ON pr.ProductID = pi.ProductID
            JOIN Production.Location l  ON l.LocationID = pi.LocationID
            WHERE pi.ModifiedDate >= @ModifiedSince
            ORDER BY pr.ProductID, l.LocationID;
            """;
    }
}