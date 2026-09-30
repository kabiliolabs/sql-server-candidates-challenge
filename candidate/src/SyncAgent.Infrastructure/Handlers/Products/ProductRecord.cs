namespace SyncAgent.Infrastructure.Handlers.Products
{
    public sealed class ProductRecord
    {
        public int ProductId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string ProductNumber { get; init; } = string.Empty;
        public string? Color { get; init; }
        public decimal StandardCost { get; init; }
        public decimal ListPrice { get; init; }
        public string? Category { get; init; }
        public string? Subcategory { get; init; }
        public DateTime ModifiedDate { get; init; }
    }
}