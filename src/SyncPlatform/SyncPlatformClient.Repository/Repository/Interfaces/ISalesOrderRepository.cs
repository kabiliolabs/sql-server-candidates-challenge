using SyncPlatformClient.Repository.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncPlatformClient.Repository.Repository.Interfaces
{
    public interface ISalesOrderRepository
    {
        Task<IEnumerable<Order>> GetOrderListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
    }
}
