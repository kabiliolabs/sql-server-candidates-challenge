using System.Text.Json.Serialization;

namespace TaskExecutionService.Models;

/// <summary>A single line item nested inside an <see cref="OrderDto"/>.</summary>
public sealed class OrderDetailDto
{
    /// <summary>Foreign key used only to group details under their order; excluded from the output payload.</summary>
    [JsonIgnore]
    public int SalesOrderId { get; init; }

    public string ProductName { get; init; } = string.Empty;
    public string ProductNumber { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public short Quantity { get; init; }
    public decimal LineTotal { get; init; }
}
