using Mediator;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Queries;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Handlers;

public sealed class GetProductsHandler(ISyncQueryRepository<ProductRecord> repository) : IRequestHandler<GetProductsQuery, IReadOnlyList<object>>
{
    public async ValueTask<IReadOnlyList<object>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetModifiedSinceAsync(request.ModifiedSince, request.MaxRows, cancellationToken);
        return records.Cast<object>().ToList();
    }
}
