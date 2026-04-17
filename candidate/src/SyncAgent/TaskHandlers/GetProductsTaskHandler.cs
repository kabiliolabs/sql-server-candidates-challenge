using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Infrastructure.Database;

namespace SyncAgent.TaskHandlers;

public sealed class GetProductsTaskHandler(IAdventureWorksReadRepository repository) : ISyncTaskHandler
{
    public string TaskType => SyncTaskType.GetProducts;

    public async Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken)
    {
        var products = await repository.GetProductsAsync(task.Parameters.ModifiedSince, cancellationToken);
        return products.Cast<object>().ToArray();
    }
}
