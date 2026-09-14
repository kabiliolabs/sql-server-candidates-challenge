using SyncAgent.Contracts;

namespace SyncAgent.Data;

/// <summary>
/// Read-only queries over AdventureWorks for the four sync task types.
/// </summary>
public interface IAdventureWorksRepository
{
    Task<List<CustomerDto>> GetCustomersAsync(DateTime modifiedSince, CancellationToken cancellationToken);
    Task<List<ProductDto>> GetProductsAsync(DateTime modifiedSince, CancellationToken cancellationToken);
    Task<List<ProductInventoryDto>> GetProductInventoryAsync(DateTime modifiedSince, CancellationToken cancellationToken);
    Task<List<OrderDto>> GetOrdersAsync(DateTime modifiedSince, CancellationToken cancellationToken);
}
