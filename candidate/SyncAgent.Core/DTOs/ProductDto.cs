namespace SyncAgent.Core.DTOs;

public class ProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string? Color { get; set; }
    public decimal StandardCost { get; set; }
    public decimal ListPrice { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public DateTime ModifiedDate { get; set; }
}
