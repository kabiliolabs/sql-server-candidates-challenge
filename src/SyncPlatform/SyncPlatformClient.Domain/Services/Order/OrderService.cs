
using SyncPlatformClient.Domain.Contracts.Orders;
using SyncPlatformClient.Domain.DTOs;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;

namespace SyncPlatformClient.Domain.Services.Order
{
    public class OrderService : IOrderService
    {
        private readonly ISalesOrderRepository _salesOrderRepository;

        public OrderService(ISalesOrderRepository salesOrderRepository)
        {
            _salesOrderRepository = salesOrderRepository;
        }

        public async Task<SyncResult> GetOrderListAsync(SyncTask task, CancellationToken cancellationToken = default)
        {
            var modifiedSinceDate = task.Parameters.TryGetValue("modifiedSince", out var date);
            if(modifiedSinceDate)
            {
                var modifiedSince = DateTimeHelper.GetModifiedDate(date);
                var orderList = await _salesOrderRepository.GetOrderListAsync(modifiedSince, cancellationToken);
                var data = orderList
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

                return SyncResult.ToSyncResult(task.TaskId, task.TaskType, data);
            }
            return new SyncResult();
        }
    }
}
