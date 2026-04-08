using SyncPlatformClient.Repository.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncPlatformClient.Repository.Repository.Interfaces
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetProductListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
    }
}
