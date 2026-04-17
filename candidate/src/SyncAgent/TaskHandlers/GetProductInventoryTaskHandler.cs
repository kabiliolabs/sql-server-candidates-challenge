using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Infrastructure.Database;

namespace SyncAgent.TaskHandlers;

public sealed class GetProductInventoryTaskHandler(IAdventureWorksReadRepository repository) : ISyncTaskHandler
{
    public string TaskType => SyncTaskType.GetProductInventory;

    public async Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken)
    {
        var productInventory = await repository.GetProductInventoryAsync(task.Parameters.ModifiedSince, cancellationToken);
        return productInventory.Cast<object>().ToArray();
    }
}
