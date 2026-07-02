using System.Data;

namespace TaskExecutionService.Data;

/// <summary>Creates open database connections, so executors don't depend on a
/// concrete provider and can be unit-tested against a fake.</summary>
public interface IDbConnectionFactory
{
    /// <summary>Opens a new connection to the configured database.</summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken);

    /// <summary>Command timeout (seconds) to apply to queries.</summary>
    int CommandTimeoutSeconds { get; }
}
