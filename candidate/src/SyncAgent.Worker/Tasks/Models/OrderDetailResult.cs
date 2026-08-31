using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Tasks.Models;

public record OrderDetailResult
{
    [JsonPropertyName("productName")]
    public string? ProductName { get; init; }

    [JsonPropertyName("productNumber")]
    public string? ProductNumber { get; init; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; init; }
}
