
using SyncPlatformClient.Domain.DTOs;

namespace SyncPlatformClient.Domain.Contracts.Products
{
    public interface IProductService
    {
        Task<SyncResult> GetProductListAsync(SyncTask task, CancellationToken cancellationToken = default);
    }
}
