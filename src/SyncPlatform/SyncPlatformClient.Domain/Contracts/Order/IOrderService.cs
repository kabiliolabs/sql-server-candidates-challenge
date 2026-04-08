
using SyncPlatformClient.Domain.DTOs;


namespace SyncPlatformClient.Domain.Contracts.Orders
{
    public interface IOrderService
    {
        Task<SyncResult> GetOrderListAsync(SyncTask task, CancellationToken cancellationToken = default);
    }
}
