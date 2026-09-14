using SyncAgent.Contracts;
using SyncAgent.Data;

namespace SyncAgent.Handlers;

public sealed class GetProductsTaskHandler(IAdventureWorksRepository repository, ILogger<GetProductsTaskHandler> logger)
    : TaskHandlerBase(logger)
{
    public override string TaskType => "GetProducts";

    protected override async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskDto task, DateTime modifiedSince, CancellationToken cancellationToken)
    {
        var products = await repository.GetProductsAsync(modifiedSince, cancellationToken);
        return products;
    }
}
