using System.Data.SqlClient;

namespace SyncAgent.Sql
{
    /// <summary>Holds the connection string; each call returns a new connection
    /// (ADO.NET pools underneath). Callers dispose.</summary>
    public sealed class SqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public SqlConnection Create()
        {
            return new SqlConnection(_connectionString);
        }
    }
}