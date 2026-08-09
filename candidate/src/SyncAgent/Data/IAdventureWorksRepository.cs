using SyncAgent.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Data;

public interface IAdventureWorksRepository
{
	Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<ProductDto>> GetProductsAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<ProductInventoryDto>> GetProductInventoryAsync(
		DateTime modifiedSince,
		CancellationToken cancellationToken);
}