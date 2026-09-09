namespace SyncAgent.Core.Models.Records
{
    /// <summary>
    /// One line of an order inside a <c>GetOrders</c> payload.
    /// </summary>
    public sealed class OrderDetailRecord
    {
        public string ProductName { get; init; } = string.Empty;

        public string ProductNumber { get; init; } = string.Empty;

        public decimal UnitPrice { get; init; }

        /// <summary>Maps to <c>SalesOrderDetail.OrderQty</c>.</summary>
        public short Quantity { get; init; }

        public decimal LineTotal { get; init; }
    }
}
