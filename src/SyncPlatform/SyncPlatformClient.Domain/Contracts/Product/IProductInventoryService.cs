
using SyncPlatformClient.Domain.DTOs;

namespace SyncPlatformClient.Domain.Contracts.Products
{
    public interface IProductInventoryService
    {
        Task<SyncResult> GetProductInventoryListAsync(SyncTask task, CancellationToken cancellationToken = default);

    }
}
