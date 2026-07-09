using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;

namespace SyncAgent.Data;

/// <summary>
/// Runs a query against the local client database with Dapper.
/// </summary>
/// <remarks>
/// A connection per query, returned to the pool immediately afterwards: the agent is idle most of
/// the time and holding a connection open across a poll interval would waste one for nothing.
/// <para>
/// The cancellation token is threaded through <see cref="CommandDefinition"/> rather than only
/// awaited on, so host shutdown actually cancels an in-flight query instead of waiting for it.
/// </para>
/// </remarks>
internal sealed class SqlQueryExecutor : ISqlQueryExecutor
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;

    public SqlQueryExecutor(IOptions<DatabaseOptions> databaseOptions, IOptions<AgentOptions> agentOptions)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(agentOptions);

        _connectionString = databaseOptions.Value.AdventureWorks;
        _commandTimeoutSeconds = agentOptions.Value.CommandTimeoutSeconds;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, SqlQueryParameters parameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);

        var command = new CommandDefinition(
            sql,
            parameters,
            commandTimeout: _commandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<T>(command).ConfigureAwait(false);
        return rows.AsList();
    }
}
