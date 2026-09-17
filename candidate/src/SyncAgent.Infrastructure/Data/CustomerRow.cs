namespace SyncAgent.Infrastructure.Data;

/// <summary>Exact shape of CustomerQueries.SelectModifiedSince — deliberately has no Phone
/// property, so the "no phone data available" decision has to be made explicitly when mapping
/// to the public CustomerRecord, not quietly inside the SQL.</summary>
internal sealed record CustomerRow(
    int CustomerId,
    string AccountNumber,
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? AddressLine1,
    string? City,
    string? StateProvince,
    string? PostalCode,
    string? CountryRegion);
