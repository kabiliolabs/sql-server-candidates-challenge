namespace SyncAgent.Data.Entities;

public class ProductSubcategory
{
    public int ProductSubcategoryId { get; set; }
    public string? Name { get; set; }
    public int ProductCategoryId { get; set; }

    public ProductCategory ProductCategory { get; set; } = null!;
}
