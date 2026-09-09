using System;

namespace SyncAgent.Core.Models.Records
{
    /// <summary>
    /// One row of a <c>GetProducts</c> payload.
    /// </summary>
    public sealed class ProductRecord
    {
        public int ProductId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string ProductNumber { get; init; } = string.Empty;

        public string? Color { get; init; }

        public decimal StandardCost { get; init; }

        public decimal ListPrice { get; init; }

        /// <summary>Null for products that are not assigned to a subcategory.</summary>
        public string? Category { get; init; }

        /// <summary>Null for products that are not assigned to a subcategory.</summary>
        public string? Subcategory { get; init; }

        /// <summary>
        /// Always <see cref="DateTimeKind.Utc"/>. AdventureWorks stores <c>datetime</c>
        /// without a zone; the contract renders these with a trailing Z, so the agent treats
        /// the stored values as UTC rather than inventing a conversion.
        /// </summary>
        public DateTime ModifiedDate { get; init; }
    }
}
