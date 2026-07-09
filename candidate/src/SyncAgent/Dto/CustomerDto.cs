namespace SyncAgent.Dto;

/// <summary>
/// One row of <c>GetCustomers</c>. Property names are the contract: they become camelCase JSON.
/// </summary>
/// <remarks>
/// 701 of the 19,820 customers are stores with no associated person, so the name, email and phone
/// fields are genuinely nullable. The customer contract has no company-name field to fall back to;
/// see Known Limitations in the submission.
/// </remarks>
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
