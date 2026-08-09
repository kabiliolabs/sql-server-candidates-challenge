using SyncAgent.Data;
using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Handlers;

public sealed class GetProductInventoryHandler
    : ISyncTaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetProductInventoryHandler(
        IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetProductInventory";

    public async Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        var modifiedSince =
            SyncTaskParameterHelper.GetModifiedSinceUtc(task);

        var inventory =
            await _repository.GetProductInventoryAsync(
                modifiedSince,
                cancellationToken);

        return new SyncTaskExecution(
            inventory,
            inventory.Count);
    }
}