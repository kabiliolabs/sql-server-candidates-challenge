namespace SyncAgent.Data.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string? Name { get; set; }
    public string? ProductNumber { get; set; }
    public string? Color { get; set; }
    public decimal StandardCost { get; set; }
    public decimal ListPrice { get; set; }
    public int? ProductSubcategoryId { get; set; }
    public DateTime ModifiedDate { get; set; }

    public ProductSubcategory? ProductSubcategory { get; set; }
}
