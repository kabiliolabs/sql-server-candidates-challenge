using Microsoft.Data.SqlClient;

namespace SyncAgent.Worker.Data;

public class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    private const string ConnectionStringName = "AdventureWorks";

    private readonly string _connectionString = GetRequiredConnectionString(configuration);

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing required configuration: ConnectionStrings:{ConnectionStringName}. " +
                "Set it for local development with:\n" +
                $"  dotnet user-secrets set \"ConnectionStrings:{ConnectionStringName}\" \"<your connection string>\"");
        }

        return connectionString;
    }
}
