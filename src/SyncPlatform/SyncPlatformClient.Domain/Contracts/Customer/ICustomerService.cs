
using SyncPlatformClient.Domain.DTOs;

namespace SyncPlatformClient.Domain.Contracts.Customers
{
    public interface ICustomerService
    {
        Task<SyncResult> GetCustomerListAsync(SyncTask task, CancellationToken cancellationToken = default);
    }
}
