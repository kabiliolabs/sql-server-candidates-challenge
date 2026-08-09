using SyncAgent.Data;
using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Handlers;

public sealed class GetOrdersHandler
    : ISyncTaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetOrdersHandler(
        IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetOrders";

    public async Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        var modifiedSince =
            SyncTaskParameterHelper.GetModifiedSinceUtc(task);

        var orders =
            await _repository.GetOrdersAsync(
                modifiedSince,
                cancellationToken);

        return new SyncTaskExecution(
            orders,
            orders.Count);
    }
}