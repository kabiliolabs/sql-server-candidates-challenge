namespace SyncAgent.Data.Entities;

public class SalesOrderHeader
{
    public int SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public byte Status { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalDue { get; set; }
    public DateTime ModifiedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public List<SalesOrderDetail> SalesOrderDetails { get; set; } = new();
}
