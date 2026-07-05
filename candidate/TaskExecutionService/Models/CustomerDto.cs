namespace TaskExecutionService.Models;

/// <summary>Customer row returned by the GetCustomers task. Property names are
/// serialized to camelCase and must match the platform contract exactly.</summary>
public sealed class CustomerDto
{
    public int CustomerId { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? EmailAddress { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? StateProvince { get; init; }
    public string? PostalCode { get; init; }
    public string? CountryRegion { get; init; }
}
