namespace SyncAgent.Dto;

/// <summary>One order of <c>GetOrders</c>, with its line items nested.</summary>
public sealed record OrderDto
{
    public int SalesOrderId { get; init; }

    public DateTime OrderDate { get; init; }

    /// <summary>The order status code (tinyint in the database, cast to int in SQL).</summary>
    public int Status { get; init; }

    /// <summary>
    /// The person's name when the customer has one, otherwise the store's name. Null only if the
    /// customer has neither, which the schema does not currently allow.
    /// </summary>
    public string? CustomerName { get; init; }

    public string AccountNumber { get; init; } = string.Empty;

    public decimal TotalDue { get; init; }

    public IReadOnlyList<OrderDetailDto> OrderDetails { get; init; } = [];
}
