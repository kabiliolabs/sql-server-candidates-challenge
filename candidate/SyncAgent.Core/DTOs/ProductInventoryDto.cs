namespace SyncAgent.Core.DTOs;

public class ProductInventoryDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public byte Bin { get; set; }
    public short Quantity { get; set; }
    public DateTime ModifiedDate { get; set; }
}
