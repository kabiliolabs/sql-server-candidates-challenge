using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Infrastructure.Database;

namespace SyncAgent.TaskHandlers;

public sealed class GetCustomersTaskHandler(IAdventureWorksReadRepository repository) : ISyncTaskHandler
{
    public string TaskType => SyncTaskType.GetCustomers;

    public async Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken)
    {
        var customers = await repository.GetCustomersAsync(task.Parameters.ModifiedSince, cancellationToken);
        return customers.Cast<object>().ToArray();
    }
}
