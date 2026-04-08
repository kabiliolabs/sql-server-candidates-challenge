
using SyncPlatformClient.Domain.Contracts.Products;
using SyncPlatformClient.Domain.DTOs;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;

namespace SyncPlatformClient.Domain.Services.Product
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<SyncResult> GetProductListAsync(SyncTask task, CancellationToken cancellationToken = default)
        {
            var modifiedSinceDate = task.Parameters.TryGetValue("modifiedSince", out var date);
            if(modifiedSinceDate)
            {
                var modifiedSince = DateTimeHelper.GetModifiedDate(date);
                var productList = await _productRepository.GetProductListAsync(modifiedSince, cancellationToken);
                var data = productList.Select(p => (object)new
                {
                    productId = p.ProductId,
                    name = p.Name,
                    productNumber = p.ProductNumber,
                    color = p.Color,
                    standardCost = p.StandardCost,
                    listPrice = p.ListPrice,
                    category = p.Category,
                    subcategory = p.Subcategory,
                    modifiedDate = p.ModifiedDate
                }).ToList();

                return SyncResult.ToSyncResult(task.TaskId, task.TaskType, data);
            }
            return new SyncResult();
        }
    }
}
