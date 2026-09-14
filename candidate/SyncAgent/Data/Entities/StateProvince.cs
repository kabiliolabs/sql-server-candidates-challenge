namespace SyncAgent.Data.Entities;

public class StateProvince
{
    public int StateProvinceId { get; set; }
    public string? Name { get; set; }
    public string CountryRegionCode { get; set; } = string.Empty;

    public CountryRegion CountryRegion { get; set; } = null!;
}
