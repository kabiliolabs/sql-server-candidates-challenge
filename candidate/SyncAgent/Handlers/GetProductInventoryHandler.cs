using SyncAgent.Data;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent.Handlers;

public class GetProductInventoryHandler : ITaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetProductInventoryHandler(IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetProductInventory";

    public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default)
    {
        var modifiedSince = task.GetModifiedSince();
        var inventory = await _repository.GetProductInventoryAsync(modifiedSince, cancellationToken);

        var data = inventory.Select(i => (object)new
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

        return SyncResult.Success(task.TaskId, task.TaskType, data);
    }
}
