namespace TaskExecutionService.Models;

/// <summary>Order header (with its line items) returned by the GetOrders task.</summary>
public sealed class OrderDto
{
    public int SalesOrderId { get; init; }
    public DateTime OrderDate { get; init; }
    public byte Status { get; init; }
    public string? CustomerName { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public decimal TotalDue { get; init; }
    public IReadOnlyList<OrderDetailDto> OrderDetails { get; set; } = Array.Empty<OrderDetailDto>();
}
