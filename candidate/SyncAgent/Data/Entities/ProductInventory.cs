namespace SyncAgent.Data.Entities;

public class ProductInventory
{
    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public string? Shelf { get; set; }
    public byte Bin { get; set; }
    public short Quantity { get; set; }
    public DateTime ModifiedDate { get; set; }

    public Product Product { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
