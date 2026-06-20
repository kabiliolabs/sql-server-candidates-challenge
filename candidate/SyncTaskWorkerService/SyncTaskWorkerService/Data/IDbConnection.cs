using Microsoft.Data.SqlClient;

namespace SyncTaskWorkerService.Data
{
    public interface IDbConnection
    {
        SqlConnection Create();
    }
}
