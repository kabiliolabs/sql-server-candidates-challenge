using SyncAgent.Data;
using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Handlers;

public sealed class GetProductsHandler
    : ISyncTaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetProductsHandler(
        IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetProducts";

    public async Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        var modifiedSince =
            SyncTaskParameterHelper.GetModifiedSinceUtc(task);

        var products =
            await _repository.GetProductsAsync(
                modifiedSince,
                cancellationToken);

        return new SyncTaskExecution(
            products,
            products.Count);
    }
}