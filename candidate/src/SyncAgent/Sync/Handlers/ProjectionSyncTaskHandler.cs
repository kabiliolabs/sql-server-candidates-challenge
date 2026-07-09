using SyncAgent.Data;

namespace SyncAgent.Sync.Handlers;

/// <summary>
/// Base for a sync task whose rows map one-to-one onto DTOs, with no reshaping.
/// </summary>
/// <remarks>
/// Three of the four task types are exactly this: run a constant query with the two bound
/// parameters, return the rows. A concrete handler supplies only its task type and its SQL, which
/// is what keeps "a new task type is one class plus one registration" honest rather than aspirational.
/// <para>
/// The <see cref="IReadOnlyList{T}"/> returned by the executor converts to
/// <c>IReadOnlyList&lt;object&gt;</c> without copying, because the interface is covariant in
/// <typeparamref name="TRow"/>.
/// </para>
/// </remarks>
/// <typeparam name="TRow">The DTO the query's columns are mapped onto.</typeparam>
internal abstract class ProjectionSyncTaskHandler<TRow>(ISqlQueryExecutor queryExecutor) : ISyncTaskHandler
    where TRow : class
{
    private readonly ISqlQueryExecutor _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));

    public abstract string TaskType { get; }

    protected abstract string Sql { get; }

    public async Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var rows = await _queryExecutor
            .QueryAsync<TRow>(Sql, parameters.ToSqlParameters(), cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }
}
