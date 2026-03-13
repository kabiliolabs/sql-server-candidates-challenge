using SyncAgent.Data;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent.Handlers;

public class GetOrdersHandler : ITaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetOrdersHandler(IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetOrders";

    public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default)
    {
        var modifiedSince = task.GetModifiedSince();
        var flatRows = await _repository.GetOrderFlatRowsAsync(modifiedSince, cancellationToken);

        // Group flat rows into orders with nested orderDetails
        var data = flatRows
            .GroupBy(r => r.SalesOrderId)
            .Select(g =>
            {
                var first = g.First();
                return (object)new
                {
                    salesOrderId = first.SalesOrderId,
                    orderDate = first.OrderDate,
                    status = first.Status,
                    customerName = first.CustomerName,
                    accountNumber = first.AccountNumber,
                    totalDue = first.TotalDue,
                    orderDetails = g.Select(d => new
                    {
                        productName = d.ProductName,
                        productNumber = d.ProductNumber,
                        unitPrice = d.UnitPrice,
                        quantity = d.Quantity,
                        lineTotal = d.LineTotal
                    }).ToList()
                };
            })
            .Cast<object>()
            .ToList();

        return SyncResult.Success(task.TaskId, task.TaskType, data);
    }
}
