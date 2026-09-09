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
    /// <inheritdoc cref="IProductQuery" />
    public sealed class ProductQuery : IProductQuery
    {
        /*
            Category and subcategory reach Product through a nullable ProductSubcategoryID, and
            209 of the 504 products have none — components and assemblies that are not sold on
            their own. Both joins are therefore LEFT: an inner join would silently drop nearly
            half the catalogue, and a sync that is quietly missing 41% of its rows is worse
            than one that reports them with a null category.
        */
        private const string Sql = @"
SELECT TOP (@maxRecords)
    p.ProductID,
    p.Name,
    p.ProductNumber,
    p.Color,
    p.StandardCost,
    p.ListPrice,
    pc.Name AS Category,
    psc.Name AS Subcategory,
    p.ModifiedDate
FROM Production.Product AS p
LEFT JOIN Production.ProductSubcategory AS psc
    ON psc.ProductSubcategoryID = p.ProductSubcategoryID
LEFT JOIN Production.ProductCategory AS pc
    ON pc.ProductCategoryID = psc.ProductCategoryID
WHERE p.ModifiedDate >= @modifiedSince
ORDER BY p.ProductID;";

        // Ordinals in SELECT order; the reader is sequential-access.
        private const int OrdinalProductId = 0;
        private const int OrdinalName = 1;
        private const int OrdinalProductNumber = 2;
        private const int OrdinalColor = 3;
        private const int OrdinalStandardCost = 4;
        private const int OrdinalListPrice = 5;
        private const int OrdinalCategory = 6;
        private const int OrdinalSubcategory = 7;
        private const int OrdinalModifiedDate = 8;

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly SyncAgentOptions _options;

        public ProductQuery(ISqlConnectionFactory connectionFactory, SyncAgentOptions options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyList<ProductRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            var records = new List<ProductRecord>();

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
                    records.Add(new ProductRecord
                    {
                        ProductId = reader.GetInt32(OrdinalProductId),
                        Name = reader.GetString(OrdinalName),
                        ProductNumber = reader.GetString(OrdinalProductNumber),
                        Color = reader.GetNullableString(OrdinalColor),
                        StandardCost = reader.GetDecimal(OrdinalStandardCost),
                        ListPrice = reader.GetDecimal(OrdinalListPrice),
                        Category = reader.GetNullableString(OrdinalCategory),
                        Subcategory = reader.GetNullableString(OrdinalSubcategory),
                        ModifiedDate = reader.GetUtcDateTime(OrdinalModifiedDate),
                    });
                }
            }

            return records;
        }
    }
}
