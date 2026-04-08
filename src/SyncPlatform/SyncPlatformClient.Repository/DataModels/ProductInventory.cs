

namespace SyncPlatformClient.Repository.DataModels
{
    public class ProductInventory
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductNumber { get; set; }
        public string LocationName { get; set; }
        public string Shelf { get; set; }
        public byte Bin { get; set; }
        public short Quantity { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
