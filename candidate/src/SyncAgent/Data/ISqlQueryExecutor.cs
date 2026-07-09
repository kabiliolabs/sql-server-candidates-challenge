namespace SyncAgent.Data;

/// <summary>
/// The seam between a handler and the database. Handlers name a query and its parameters; they
/// never open a connection, and they never see a <c>DbCommand</c>.
/// </summary>
public interface ISqlQueryExecutor
{
    /// <param name="sql">A compile-time constant from <c>SyncQueries</c>. Never composed at run time.</param>
    /// <param name="parameters">Bound as <c>DbParameter</c>s by the implementation.</param>
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, SqlQueryParameters parameters, CancellationToken cancellationToken);
}
