using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TaskExecutionService.Configuration;

namespace TaskExecutionService.Data;

/// <summary>SQL Server implementation of <see cref="IDbConnectionFactory"/>.</summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseOptions _options;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is not configured. Set it in appsettings.json, user-secrets, or an environment variable.");
        }
    }

    public int CommandTimeoutSeconds => _options.CommandTimeoutSeconds;

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
