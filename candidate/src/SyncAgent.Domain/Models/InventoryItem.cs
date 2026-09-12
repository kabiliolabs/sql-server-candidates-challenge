namespace SyncAgent.Domain.Models;

public sealed class InventoryItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int Bin { get; set; }
    public int Quantity { get; set; }
    public DateTime ModifiedDate { get; set; }
}
