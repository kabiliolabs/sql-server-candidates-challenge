namespace SyncAgent.Domain.Models;

public sealed class Order
{
    public int SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public int Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal TotalDue { get; set; }
    public List<OrderLine> OrderDetails { get; set; } = [];
}
