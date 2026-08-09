using SyncAgent.Data;
using SyncAgent.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Handlers;

public sealed class GetCustomersHandler
    : ISyncTaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetCustomersHandler(
        IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetCustomers";

    public async Task<SyncTaskExecution> ExecuteAsync(
        SyncTask task,
        CancellationToken cancellationToken)
    {
        var modifiedSince =
            SyncTaskParameterHelper.GetModifiedSinceUtc(task);

        var customers =
            await _repository.GetCustomersAsync(
                modifiedSince,
                cancellationToken);

        return new SyncTaskExecution(
            customers,
            customers.Count);
    }
}