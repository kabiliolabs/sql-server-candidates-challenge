using SyncAgent.Dto;

namespace SyncAgent.Sync.Handlers;

/// <summary>
/// Folds the flat header-joined-to-detail rows of <c>GetOrders</c> into one
/// <see cref="OrderDto"/> per order, with its line items nested.
/// </summary>
/// <remarks>
/// Orders are keyed rather than assumed adjacent. The query does order by SalesOrderID, so a
/// simple "start a new order whenever the id changes" pass would work today -- but it would fail
/// silently and produce duplicate orders the day someone changes the ORDER BY. Keying costs a
/// dictionary lookup per row and cannot break that way.
/// <para>
/// First-seen order of orders, and source order of line items within an order, are both preserved,
/// so the output is a deterministic function of the query's ORDER BY.
/// </para>
/// </remarks>
internal static class OrderResultBuilder
{
    public static IReadOnlyList<OrderDto> Build(IEnumerable<OrderRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var detailsByOrder = new Dictionary<int, List<OrderDetailDto>>();
        var headers = new List<OrderRow>();

        foreach (var row in rows)
        {
            if (!detailsByOrder.TryGetValue(row.SalesOrderId, out var details))
            {
                details = [];
                detailsByOrder.Add(row.SalesOrderId, details);

                // The header columns repeat on every line of an order; the first one wins.
                headers.Add(row);
            }

            details.Add(new OrderDetailDto
            {
                ProductName = row.ProductName,
                ProductNumber = row.ProductNumber,
                UnitPrice = row.UnitPrice,
                Quantity = row.Quantity,
                LineTotal = row.LineTotal,
            });
        }

        return [.. headers.Select(header => new OrderDto
        {
            SalesOrderId = header.SalesOrderId,
            OrderDate = header.OrderDate,
            Status = header.Status,
            CustomerName = header.CustomerName,
            AccountNumber = header.AccountNumber,
            TotalDue = header.TotalDue,
            OrderDetails = detailsByOrder[header.SalesOrderId],
        })];
    }
}
