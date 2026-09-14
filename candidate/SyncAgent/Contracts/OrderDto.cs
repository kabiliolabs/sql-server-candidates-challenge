using System.Text.Json.Serialization;

namespace SyncAgent.Contracts;

public sealed class OrderDto
{
    [JsonPropertyName("salesOrderId")]
    public int SalesOrderId { get; set; }

    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; set; }

    [JsonPropertyName("status")]
    public byte Status { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("totalDue")]
    public decimal TotalDue { get; set; }

    [JsonPropertyName("orderDetails")]
    public List<OrderDetailDto> OrderDetails { get; set; } = new();
}
