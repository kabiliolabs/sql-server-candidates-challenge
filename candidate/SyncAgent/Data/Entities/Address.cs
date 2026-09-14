namespace SyncAgent.Data.Entities;

public class Address
{
    public int AddressId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public int StateProvinceId { get; set; }
    public string? PostalCode { get; set; }

    public StateProvince StateProvince { get; set; } = null!;
}
