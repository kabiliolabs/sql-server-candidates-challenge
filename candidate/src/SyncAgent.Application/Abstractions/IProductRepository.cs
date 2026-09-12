using SyncAgent.Domain.Models;

namespace SyncAgent.Application.Abstractions;

public interface IProductRepository
{
    Task<List<Product>> GetProductsAsync(DateTime modifiedSince, CancellationToken cancellationToken);
}
