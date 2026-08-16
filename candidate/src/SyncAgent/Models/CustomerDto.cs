namespace SyncAgent.Models;

public sealed record CustomerDto
{
    public int CustomerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? EmailAddress { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? StateProvince { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryRegion { get; init; }
}