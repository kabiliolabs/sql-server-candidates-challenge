using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Contracts;

namespace SyncAgent.Application.Services;

public sealed class GetOrdersHandler(IOrderRepository repository) : SyncTaskHandlerBase<OrderRecord>
{
    public override string TaskType => "GetOrders";

    protected override async Task<IReadOnlyList<OrderRecord>> FetchRecordsAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken)
    {
        var orders = await repository.GetOrdersAsync(modifiedSince, cancellationToken);

        return orders.Select(order => new OrderRecord
        {
            SalesOrderId = order.SalesOrderId,
            OrderDate = order.OrderDate,
            Status = order.Status,
            CustomerName = order.CustomerName,
            AccountNumber = order.AccountNumber,
            TotalDue = order.TotalDue,
            OrderDetails = order.OrderDetails.Select(line => new OrderLineRecord
            {
                ProductName = line.ProductName,
                ProductNumber = line.ProductNumber,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                LineTotal = line.LineTotal
            }).ToList()
        }).ToList();
    }
}
