namespace SyncAgent.Infrastructure.Handlers.Orders
{
    public sealed class OrderRecord
    {
        public int SalesOrderId { get; init; }
        public DateTime OrderDate { get; init; }
        public byte Status { get; init; }
        public string? CustomerName { get; init; }
        public string AccountNumber { get; init; } = string.Empty;
        public decimal TotalDue { get; init; }
        public IReadOnlyList<OrderDetailRecord> OrderDetails { get; set; } = [];
    }

    public sealed class OrderDetailRecord
    {
        public string ProductName { get; init; } = string.Empty;
        public string ProductNumber { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public short Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }

    internal sealed class OrderDetailRow
    {
        public int SalesOrderId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string ProductNumber { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public short Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }
}