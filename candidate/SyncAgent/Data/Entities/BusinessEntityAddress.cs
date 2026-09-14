namespace SyncAgent.Data.Entities;

public class BusinessEntityAddress
{
    public int BusinessEntityId { get; set; }
    public int AddressId { get; set; }

    public Address Address { get; set; } = null!;
}
