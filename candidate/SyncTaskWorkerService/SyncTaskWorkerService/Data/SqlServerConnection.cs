
using Microsoft.Data.SqlClient;


namespace SyncTaskWorkerService.Data
{
    public class SqlServerConnection(string _connectionString) : IDbConnection
    {
        public SqlConnection Create()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
