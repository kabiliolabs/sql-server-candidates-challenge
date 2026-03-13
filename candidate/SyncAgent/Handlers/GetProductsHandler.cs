using SyncAgent.Data;
using SyncAgent.Models;
using SyncAgent.Services;

namespace SyncAgent.Handlers;

public class GetProductsHandler : ITaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public GetProductsHandler(IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public string TaskType => "GetProducts";

    public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken = default)
    {
        var modifiedSince = task.GetModifiedSince();
        var products = await _repository.GetProductsAsync(modifiedSince, cancellationToken);

        var data = products.Select(p => (object)new
        {
            productId = p.ProductId,
            name = p.Name,
            productNumber = p.ProductNumber,
            color = p.Color,
            standardCost = p.StandardCost,
            listPrice = p.ListPrice,
            category = p.Category,
            subcategory = p.Subcategory,
            modifiedDate = p.ModifiedDate
        }).ToList();

        return SyncResult.Success(task.TaskId, task.TaskType, data);
    }
}
