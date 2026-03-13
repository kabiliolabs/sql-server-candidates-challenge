using SyncAgent.Data.Dto;

namespace SyncAgent.Data;

public interface IAdventureWorksRepository
{
    Task<IEnumerable<CustomerDto>> GetCustomersAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductDto>> GetProductsAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderFlatRow>> GetOrderFlatRowsAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductInventoryDto>> GetProductInventoryAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default);
}
