namespace SyncAgent.Dto;

/// <summary>
/// One flat header-joined-to-detail row as SQL Server returns it, before
/// <c>OrderResultBuilder</c> folds the repeated header columns into a single
/// <see cref="OrderDto"/> per order.
/// </summary>
/// <remarks>
/// Querying flat and grouping in memory, rather than issuing one query per order, keeps
/// <c>GetOrders</c> to a single round trip. The header columns repeat once per line item, which is
/// a few thousand duplicated strings -- cheaper than 5,000 round trips.
/// </remarks>
public sealed record OrderRow
{
    public int SalesOrderId { get; init; }

    public DateTime OrderDate { get; init; }

    public int Status { get; init; }

    public string? CustomerName { get; init; }

    public string AccountNumber { get; init; } = string.Empty;

    public decimal TotalDue { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductNumber { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}
