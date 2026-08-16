using SyncAgent.Repository;
using SyncAgent.Models;
namespace SyncAgent.Services;

public sealed class SyncTaskDispatcher : ISyncTaskDispatcher
{
    private readonly IAdventureWorksRepository _repository;

    public SyncTaskDispatcher(
        IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public async Task<SyncTaskExecution> ExecuteAsync(SyncTask task, CancellationToken cancellationToken)
    {
        var modifiedSince = task.Parameters.ModifiedSince?.UtcDateTime
            ?? throw new ArgumentException(
                "modifiedSince is required.");

        // Strategy selection: each task type maps to its strategy (the repository query).
        // If the set of task types grows or becomes dynamic, replace this switch with
        // an explicit strategy registry.
        IReadOnlyList<object> data = task.TaskType switch
        {
            "GetCustomers" =>
                await _repository.GetCustomersAsync(modifiedSince, cancellationToken),
            "GetProducts" =>
                await _repository.GetProductsAsync(modifiedSince, cancellationToken),
            "GetOrders" =>
                await _repository.GetOrdersAsync(modifiedSince, cancellationToken),
            "GetProductInventory" =>
                await _repository.GetProductInventoryAsync(modifiedSince, cancellationToken),
            _ =>
                throw new NotSupportedException($"Unsupported task type: {task.TaskType}")
        };

        return new SyncTaskExecution(data, data.Count);

    }
}
