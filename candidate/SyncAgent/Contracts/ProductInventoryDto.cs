using System.Text.Json.Serialization;

namespace SyncAgent.Contracts;

public sealed class ProductInventoryDto
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("productNumber")]
    public string? ProductNumber { get; set; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [JsonPropertyName("shelf")]
    public string? Shelf { get; set; }

    [JsonPropertyName("bin")]
    public byte Bin { get; set; }

    [JsonPropertyName("quantity")]
    public short Quantity { get; set; }

    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; set; }
}
