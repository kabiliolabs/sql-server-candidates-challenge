using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Core.Handlers;

// All four handlers in this folder end up the same shape (fetch via the repository, cast to
// object). Left as four explicit classes rather than one generic handler: it matches the
// "abstract at the second occurrence, not the first" call made during planning for the
// dispatcher/handler layer, stays trivially readable, and a 5th task type is free to diverge
// (e.g. need extra validation) without fighting a shared base.
public sealed class GetCustomersHandler(ISyncQueryRepository<CustomerRecord> repository, IOptions<SyncAgentOptions> options) : ISyncTaskHandler
{
    public string TaskType => TaskTypes.GetCustomers;

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskRequest request, CancellationToken cancellationToken)
    {
        var records = await repository.GetModifiedSinceAsync(request.ModifiedSince, options.Value.MaxRowsPerTask, cancellationToken);
        return records.Cast<object>().ToList();
    }
}
