namespace SyncAgent.Core.DTOs;

public class OrderDto
{
    public int SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public byte Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal TotalDue { get; set; }
    public List<OrderDetailDto> OrderDetails { get; set; } = [];
}
