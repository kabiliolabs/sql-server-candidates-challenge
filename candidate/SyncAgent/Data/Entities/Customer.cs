namespace SyncAgent.Data.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string? AccountNumber { get; set; }
    public int? PersonId { get; set; }
    public int? StoreId { get; set; }
    public DateTime ModifiedDate { get; set; }

    public Person? Person { get; set; }
    public Store? Store { get; set; }
    public List<SalesOrderHeader> SalesOrderHeaders { get; set; } = new();
}
