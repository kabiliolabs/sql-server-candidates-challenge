using SyncAgent.Data;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>
/// Records the SQL and parameters a handler asked for, and returns rows the test supplied.
/// A generic method with a constrained return type is awkward to express as a mock expectation,
/// and what these tests assert -- "which constant, bound how" -- reads better as recorded state.
/// </summary>
internal sealed class RecordingSqlQueryExecutor : ISqlQueryExecutor
{
    private readonly object _rows;

    public RecordingSqlQueryExecutor() => _rows = new object();

    public RecordingSqlQueryExecutor(object rows) => _rows = rows;

    public string? LastSql { get; private set; }

    public SqlQueryParameters? LastParameters { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, SqlQueryParameters parameters, CancellationToken cancellationToken)
    {
        CallCount++;
        LastSql = sql;
        LastParameters = parameters;
        LastCancellationToken = cancellationToken;

        return Task.FromResult(_rows as IReadOnlyList<T> ?? []);
    }
}
