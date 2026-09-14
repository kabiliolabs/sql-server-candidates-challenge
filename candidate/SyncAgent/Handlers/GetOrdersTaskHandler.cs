using SyncAgent.Contracts;
using SyncAgent.Data;

namespace SyncAgent.Handlers;

public sealed class GetOrdersTaskHandler(IAdventureWorksRepository repository, ILogger<GetOrdersTaskHandler> logger)
    : TaskHandlerBase(logger)
{
    public override string TaskType => "GetOrders";

    protected override async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskDto task, DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var orders = await repository.GetOrdersAsync(modifiedSince, cancellationToken);
        return orders;
    }
}
