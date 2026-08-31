using System.Text.Json.Serialization;

namespace SyncAgent.Worker.Tasks.Models;

public record OrderResult
{
    [JsonPropertyName("salesOrderId")]
    public int SalesOrderId { get; init; }

    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; init; }

    [JsonPropertyName("status")]
    public byte Status { get; init; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; init; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("totalDue")]
    public decimal TotalDue { get; init; }

    [JsonPropertyName("orderDetails")]
    public IReadOnlyList<OrderDetailResult> OrderDetails { get; init; } = [];
}
