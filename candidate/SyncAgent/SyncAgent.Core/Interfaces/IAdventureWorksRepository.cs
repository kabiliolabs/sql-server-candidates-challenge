using SyncAgent.Core.Models;

namespace SyncAgent.Core.Interfaces;

public interface IAdventureWorksRepository
{
    Task<IEnumerable<CustomerDto>> GetCustomersAsync(DateTime? modifiedSince, CancellationToken ct = default);
    Task<IEnumerable<ProductDto>> GetProductsAsync(DateTime? modifiedSince, CancellationToken ct = default);
    Task<IEnumerable<OrderDto>> GetOrdersAsync(DateTime? modifiedSince, CancellationToken ct = default);
    Task<IEnumerable<ProductInventoryDto>> GetProductInventoryAsync(DateTime? modifiedSince, CancellationToken ct = default);
}
