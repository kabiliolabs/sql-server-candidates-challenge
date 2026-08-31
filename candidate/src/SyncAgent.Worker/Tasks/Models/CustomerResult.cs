using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Tasks.Models;

public record CustomerResult
{
    [JsonPropertyName("customerId")]
    public int CustomerId { get; init; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("stateProvince")]
    public string? StateProvince { get; init; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("countryRegion")]
    public string? CountryRegion { get; init; }
}
