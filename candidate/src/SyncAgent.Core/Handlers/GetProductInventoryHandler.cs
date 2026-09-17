using Mediator;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Queries;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Handlers;

public sealed class GetProductInventoryHandler(ISyncQueryRepository<ProductInventoryRecord> repository) : IRequestHandler<GetProductInventoryQuery, IReadOnlyList<object>>
{
    public async ValueTask<IReadOnlyList<object>> Handle(GetProductInventoryQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetModifiedSinceAsync(request.ModifiedSince, request.MaxRows, cancellationToken);
        return records.Cast<object>().ToList();
    }
}
