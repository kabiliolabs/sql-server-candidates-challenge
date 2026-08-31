using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Tasks.Models;

public record ProductInventoryResult
{
    [JsonPropertyName("productId")]
    public int ProductId { get; init; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; init; }

    [JsonPropertyName("productNumber")]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; init; }

    [JsonPropertyName("shelf")]
    public string? Shelf { get; init; }

    [JsonPropertyName("bin")]
    public int Bin { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; init; }
}
