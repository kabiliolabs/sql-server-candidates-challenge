using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Infrastructure.Data.Queries
{
    /// <inheritdoc cref="IProductInventoryQuery" />
    public sealed class ProductInventoryQuery : IProductInventoryQuery
    {
        /*
            The grain of Production.ProductInventory is (ProductID, LocationID), so a product
            stocked in three locations produces three records. That is what the contract
            describes — it carries locationName, shelf and bin per row — so the rows are
            emitted as they are rather than being folded into one record per product.

            Both joins are INNER: the foreign keys to Product and Location are not nullable, so
            a LEFT JOIN would only add the suggestion that a stock row without a product is
            possible. If one ever appeared it would mean referential corruption, and losing it
            from the sync is the right outcome.

            ORDER BY (ProductID, LocationID) rather than the clustered key alone, so a
            re-running sync produces rows in the same order every time and the payload can be
            diffed between runs.
        */
        private const string Sql = @"
SELECT TOP (@maxRecords)
    inv.ProductID,
    p.Name AS ProductName,
    p.ProductNumber,
    l.Name AS LocationName,
    inv.Shelf,
    inv.Bin,
    inv.Quantity,
    inv.ModifiedDate
FROM Production.ProductInventory AS inv
INNER JOIN Production.Product AS p
    ON p.ProductID = inv.ProductID
INNER JOIN Production.Location AS l
    ON l.LocationID = inv.LocationID
WHERE inv.ModifiedDate >= @modifiedSince
ORDER BY inv.ProductID, inv.LocationID;";

        // Ordinals in SELECT order; the reader is sequential-access.
        private const int OrdinalProductId = 0;
        private const int OrdinalProductName = 1;
        private const int OrdinalProductNumber = 2;
        private const int OrdinalLocationName = 3;
        private const int OrdinalShelf = 4;
        private const int OrdinalBin = 5;
        private const int OrdinalQuantity = 6;
        private const int OrdinalModifiedDate = 7;

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly SyncAgentOptions _options;

        public ProductInventoryQuery(ISqlConnectionFactory connectionFactory, SyncAgentOptions options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyList<ProductInventoryRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            var records = new List<ProductInventoryRecord>();

            using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
            using (var command = SqlCommandFactory.Create(Sql, connection, _options, modifiedSince, maxRecords))
            using (var reader = await command
                       .ExecuteReaderAsync(
                           CommandBehavior.SequentialAccess | CommandBehavior.SingleResult,
                           cancellationToken)
                       .ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    records.Add(new ProductInventoryRecord
                    {
                        ProductId = reader.GetInt32(OrdinalProductId),
                        ProductName = reader.GetString(OrdinalProductName),
                        ProductNumber = reader.GetString(OrdinalProductNumber),
                        LocationName = reader.GetString(OrdinalLocationName),
                        Shelf = reader.GetString(OrdinalShelf),
                        Bin = reader.GetByte(OrdinalBin),
                        Quantity = reader.GetInt16(OrdinalQuantity),
                        ModifiedDate = reader.GetUtcDateTime(OrdinalModifiedDate),
                    });
                }
            }

            return records;
        }
    }
}
