using Microsoft.Data.SqlClient;

namespace SyncAgent.Worker.Data;

/// <summary>
/// Creates open connections to the AdventureWorks SQL Server database. Task handlers
/// depend on this abstraction rather than on Microsoft.Data.SqlClient directly, so they
/// stay unit-testable without a real database.
/// </summary>
public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
