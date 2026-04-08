

namespace SyncPlatformClient.Repository.DataModels
{

    public class Order
    {
        public int SalesOrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public byte Status { get; set; }
        public string CustomerName { get; set; }
        public string AccountNumber { get; set; }
        public decimal TotalDue { get; set; }
        public string ProductName { get; set; }
        public string ProductNumber { get; set; }
        public decimal UnitPrice { get; set; }
        public short Quantity { get; set; }
        public decimal LineTotal { get; set; }
    }
}
