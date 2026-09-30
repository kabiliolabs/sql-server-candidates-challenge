namespace SyncAgent.Infrastructure.Handlers.Inventory
{
    public sealed class ProductInventoryRecord
    {
        public int ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string ProductNumber { get; init; } = string.Empty;
        public string LocationName { get; init; } = string.Empty;
        public string Shelf { get; init; } = string.Empty;
        public byte Bin { get; init; }      
        public short Quantity { get; init; }
        public DateTime ModifiedDate { get; init; }
    }
}