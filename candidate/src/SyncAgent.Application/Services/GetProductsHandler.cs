using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Contracts;

namespace SyncAgent.Application.Services;

public sealed class GetProductsHandler(IProductRepository repository) : SyncTaskHandlerBase<ProductRecord>
{
    public override string TaskType => "GetProducts";

    protected override async Task<IReadOnlyList<ProductRecord>> FetchRecordsAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken)
    {
        var products = await repository.GetProductsAsync(modifiedSince, cancellationToken);

        return products.Select(product => new ProductRecord
        {
            ProductId = product.ProductId,
            Name = product.Name,
            ProductNumber = product.ProductNumber,
            Color = product.Color,
            StandardCost = product.StandardCost,
            ListPrice = product.ListPrice,
            Category = product.Category,
            Subcategory = product.Subcategory,
            ModifiedDate = product.ModifiedDate
        }).ToList();
    }
}
