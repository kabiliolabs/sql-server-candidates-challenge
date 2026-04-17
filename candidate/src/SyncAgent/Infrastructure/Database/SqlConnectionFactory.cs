using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;

namespace SyncAgent.Infrastructure.Database;

public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : ISqlConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public IDbConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
