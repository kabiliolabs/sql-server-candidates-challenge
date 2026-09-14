using SyncAgent.Contracts;
using SyncAgent.Data;

namespace SyncAgent.Handlers;

public sealed class GetProductInventoryTaskHandler(IAdventureWorksRepository repository, ILogger<GetProductInventoryTaskHandler> logger)
    : TaskHandlerBase(logger)
{
    public override string TaskType => "GetProductInventory";

    protected override async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskDto task, DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var inventory = await repository.GetProductInventoryAsync(modifiedSince, cancellationToken);
        return inventory;
    }
}
