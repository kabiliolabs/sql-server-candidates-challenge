namespace SyncAgent.Core.DTOs;

public class OrderDetailDto
{
    public string ProductName { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
