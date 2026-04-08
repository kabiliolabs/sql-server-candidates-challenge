
using SyncPlatformClient.Domain.Contracts.Products;
using SyncPlatformClient.Domain.DTOs;
using SyncPlatformClient.Repository.Repository.Interfaces;

namespace SyncPlatformClient.Domain.Services.Product
{
    public class ProductInventoryService : IProductInventoryService
    {
        private readonly IProductInventoryRepository _productInventoryRepository;

        public ProductInventoryService(IProductInventoryRepository productInventoryRepository)
        {
            _productInventoryRepository = productInventoryRepository;
        }

        public async Task<SyncResult> GetProductInventoryListAsync(SyncTask task, CancellationToken cancellationToken = default)
        {
            var modifiedSinceDate = task.Parameters.TryGetValue("modifiedSince", out var date);
            if (modifiedSinceDate)
            {
                var modifiedSince = DateTimeHelper.GetModifiedDate(date);
                var productInventoryList = await _productInventoryRepository.GetProductInventoryListAsync(modifiedSince, cancellationToken);
                var data = productInventoryList.Select(i => (object)new
                {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    productNumber = i.ProductNumber,
                    locationName = i.LocationName,
                    shelf = i.Shelf,
                    bin = i.Bin,
                    quantity = i.Quantity,
                    modifiedDate = i.ModifiedDate
                }).ToList();

                return SyncResult.ToSyncResult(task.TaskId, task.TaskType, data);

            }
            return new SyncResult();
        }
    }
}
