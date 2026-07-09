namespace SyncAgent.Dto;

/// <summary>One line item of an <see cref="OrderDto"/>.</summary>
public sealed record OrderDetailDto
{
    public string ProductName { get; init; } = string.Empty;

    public string ProductNumber { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}
