using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Handlers;

public class GetProductsHandler : ISyncTaskHandler
{
    private readonly IAdventureWorksRepository _repository;

    public string TaskType => "GetProducts";

    public GetProductsHandler(IAdventureWorksRepository repository)
    {
        _repository = repository;
    }

    public async Task<SyncResult> HandleAsync(SyncTask task, CancellationToken ct = default)
    {
        var modifiedSince = task.GetModifiedSince();
        var data = (await _repository.GetProductsAsync(modifiedSince, ct)).ToList();

        return new SyncResult
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = "completed",
            RecordCount = data.Count,
            Data = data
        };
    }
}
