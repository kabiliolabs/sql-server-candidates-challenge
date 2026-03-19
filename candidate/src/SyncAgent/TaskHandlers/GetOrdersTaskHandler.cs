using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Infrastructure.Database;

namespace SyncAgent.TaskHandlers;

public sealed class GetOrdersTaskHandler(IAdventureWorksReadRepository repository) : ISyncTaskHandler
{
    public string TaskType => SyncTaskType.GetOrders;

    public async Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken)
    {
        var orders = await repository.GetOrdersAsync(task.Parameters.ModifiedSince, cancellationToken);
        return orders.Cast<object>().ToArray();
    }
}
