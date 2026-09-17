using System.Data;
using Microsoft.Data.SqlClient;

namespace SyncAgent.Infrastructure.Data;

public interface ISqlConnectionFactory
{
    IDbConnection Create();
}

public sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public IDbConnection Create() => new SqlConnection(connectionString);
}
