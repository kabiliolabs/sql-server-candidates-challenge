using SyncAgent.Contracts;
using SyncAgent.Data;

namespace SyncAgent.Handlers;

public sealed class GetCustomersTaskHandler(IAdventureWorksRepository repository, ILogger<GetCustomersTaskHandler> logger)
    : TaskHandlerBase(logger)
{
    public override string TaskType => "GetCustomers";

    protected override async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskDto task, DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var customers = await repository.GetCustomersAsync(modifiedSince, cancellationToken);
        return customers;
    }
}
