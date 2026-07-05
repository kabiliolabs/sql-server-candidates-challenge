namespace TaskExecutionService.Models;

/// <summary>Inventory row returned by the GetProductInventory task. One row per
/// product per storage location (multiple locations per product is expected).</summary>
public sealed class ProductInventoryDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string? Shelf { get; init; }
    public int Bin { get; init; }
    public int Quantity { get; init; }
    public DateTime ModifiedDate { get; init; }
}
