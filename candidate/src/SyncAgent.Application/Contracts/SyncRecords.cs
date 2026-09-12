using System.Text.Json.Serialization;

namespace SyncAgent.Application.Contracts;

// The records the platform expects inside "data" for each task type, mirroring
// docs/sample-payloads/. Property names are pinned with [JsonPropertyName] because the platform's
// field naming is not uniform across task types: GetProducts sends "name" while
// GetProductInventory sends "productName" for the same underlying column.

public sealed record CustomerRecord
{
    [JsonPropertyName("customerId")] public required int CustomerId { get; init; }
    [JsonPropertyName("accountNumber")] public required string AccountNumber { get; init; }
    [JsonPropertyName("firstName")] public required string FirstName { get; init; }
    [JsonPropertyName("lastName")] public required string LastName { get; init; }
    [JsonPropertyName("emailAddress")] public string? EmailAddress { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("addressLine1")] public string? AddressLine1 { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("stateProvince")] public string? StateProvince { get; init; }
    [JsonPropertyName("postalCode")] public string? PostalCode { get; init; }
    [JsonPropertyName("countryRegion")] public string? CountryRegion { get; init; }
}

public sealed record ProductRecord
{
    [JsonPropertyName("productId")] public required int ProductId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("productNumber")] public required string ProductNumber { get; init; }
    [JsonPropertyName("color")] public string? Color { get; init; }
    [JsonPropertyName("standardCost")] public required decimal StandardCost { get; init; }
    [JsonPropertyName("listPrice")] public required decimal ListPrice { get; init; }
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("subcategory")] public string? Subcategory { get; init; }
    [JsonPropertyName("modifiedDate")] public required DateTime ModifiedDate { get; init; }
}

public sealed record OrderRecord
{
    [JsonPropertyName("salesOrderId")] public required int SalesOrderId { get; init; }
    [JsonPropertyName("orderDate")] public required DateTime OrderDate { get; init; }
    [JsonPropertyName("status")] public required int Status { get; init; }
    [JsonPropertyName("customerName")] public required string CustomerName { get; init; }
    [JsonPropertyName("accountNumber")] public required string AccountNumber { get; init; }
    [JsonPropertyName("totalDue")] public required decimal TotalDue { get; init; }
    [JsonPropertyName("orderDetails")] public required IReadOnlyList<OrderLineRecord> OrderDetails { get; init; }
}

public sealed record OrderLineRecord
{
    [JsonPropertyName("productName")] public required string ProductName { get; init; }
    [JsonPropertyName("productNumber")] public required string ProductNumber { get; init; }
    [JsonPropertyName("unitPrice")] public required decimal UnitPrice { get; init; }
    [JsonPropertyName("quantity")] public required int Quantity { get; init; }
    [JsonPropertyName("lineTotal")] public required decimal LineTotal { get; init; }
}

public sealed record InventoryRecord
{
    [JsonPropertyName("productId")] public required int ProductId { get; init; }
    [JsonPropertyName("productName")] public required string ProductName { get; init; }
    [JsonPropertyName("productNumber")] public required string ProductNumber { get; init; }
    [JsonPropertyName("locationName")] public required string LocationName { get; init; }
    [JsonPropertyName("shelf")] public required string Shelf { get; init; }
    [JsonPropertyName("bin")] public required int Bin { get; init; }
    [JsonPropertyName("quantity")] public required int Quantity { get; init; }
    [JsonPropertyName("modifiedDate")] public required DateTime ModifiedDate { get; init; }
}
