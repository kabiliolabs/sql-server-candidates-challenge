using System.Text.Json.Serialization;

namespace SyncAgent.Contracts;

public sealed class OrderDetailDto
{
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("productNumber")]
    public string? ProductNumber { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }
}
