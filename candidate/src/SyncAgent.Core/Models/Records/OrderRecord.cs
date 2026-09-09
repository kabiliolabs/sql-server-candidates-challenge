using System;
using System.Collections.Generic;

namespace SyncAgent.Core.Models.Records
{
    /// <summary>
    /// One row of a <c>GetOrders</c> payload: an order header with its lines nested inside.
    /// </summary>
    /// <remarks>
    /// This is the only nested payload in the contract, and the only one where recordCount
    /// counts something other than the rows the database returned — it counts orders, not
    /// order lines.
    /// </remarks>
    public sealed class OrderRecord
    {
        public int SalesOrderId { get; init; }

        /// <summary>Always <see cref="DateTimeKind.Utc"/>.</summary>
        public DateTime OrderDate { get; init; }

        /// <summary>
        /// AdventureWorks order status as a small integer (1 In process … 5 Shipped,
        /// 6 Cancelled). The contract carries the raw code rather than a label, so no lookup
        /// table is joined and no translation is invented.
        /// </summary>
        public byte Status { get; init; }

        public string? CustomerName { get; init; }

        public string AccountNumber { get; init; } = string.Empty;

        public decimal TotalDue { get; init; }

        public IReadOnlyList<OrderDetailRecord> OrderDetails { get; init; } =
            new List<OrderDetailRecord>(0);
    }
}
