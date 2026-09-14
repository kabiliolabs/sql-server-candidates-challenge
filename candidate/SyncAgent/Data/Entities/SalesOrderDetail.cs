namespace SyncAgent.Data.Entities;

public class SalesOrderDetail
{
    public int SalesOrderId { get; set; }
    public int SalesOrderDetailId { get; set; }
    public int ProductId { get; set; }
    public short OrderQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Product Product { get; set; } = null!;
}
