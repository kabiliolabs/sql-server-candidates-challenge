namespace SyncAgent.Core.Tasks;

public sealed record CustomerRecord(
    int CustomerId,
    string AccountNumber,
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? StateProvince,
    string? PostalCode,
    string? CountryRegion);
