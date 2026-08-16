namespace SyncAgent.Models;

public sealed record OrderDto
{
    public int SalesOrderId { get; init; }
    public DateTime OrderDate { get; init; }
    public byte Status { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }
    public List<OrderDetailDto> OrderDetails { get; init; } = [];
}

public sealed record OrderDetailDto
{
    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public short Quantity { get; init; }
    public decimal LineTotal { get; init; }
}