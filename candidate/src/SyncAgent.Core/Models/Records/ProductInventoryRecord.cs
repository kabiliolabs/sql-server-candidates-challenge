using System;

namespace SyncAgent.Core.Models.Records
{
    /// <summary>
    /// One row of a <c>GetProductInventory</c> payload: stock of one product at one location.
    /// </summary>
    /// <remarks>
    /// The grain is (product, location), not product. A product held in three locations
    /// produces three records, which is why the sample shows the same productId more than
    /// once in a full extract.
    /// </remarks>
    public sealed class ProductInventoryRecord
    {
        public int ProductId { get; init; }

        public string ProductName { get; init; } = string.Empty;

        public string ProductNumber { get; init; } = string.Empty;

        public string LocationName { get; init; } = string.Empty;

        public string Shelf { get; init; } = string.Empty;

        public byte Bin { get; init; }

        public short Quantity { get; init; }

        /// <summary>Always <see cref="DateTimeKind.Utc"/>.</summary>
        public DateTime ModifiedDate { get; init; }
    }
}
