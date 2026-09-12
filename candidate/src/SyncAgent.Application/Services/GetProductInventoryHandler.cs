using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Contracts;

namespace SyncAgent.Application.Services;

public sealed class GetProductInventoryHandler(IInventoryRepository repository) : SyncTaskHandlerBase<InventoryRecord>
{
    public override string TaskType => "GetProductInventory";

    protected override async Task<IReadOnlyList<InventoryRecord>> FetchRecordsAsync(
        DateTime modifiedSince,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetInventoryAsync(modifiedSince, cancellationToken);

        return items.Select(item => new InventoryRecord
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            ProductNumber = item.ProductNumber,
            LocationName = item.LocationName,
            Shelf = item.Shelf,
            Bin = item.Bin,
            Quantity = item.Quantity,
            ModifiedDate = item.ModifiedDate
        }).ToList();
    }
}
