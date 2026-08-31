using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Tasks.Models;

public record ProductResult
{
    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("productNumber")]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("standardCost")]
    public decimal StandardCost { get; init; }

    [JsonPropertyName("listPrice")]
    public decimal ListPrice { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; init; }

    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; init; }
}
