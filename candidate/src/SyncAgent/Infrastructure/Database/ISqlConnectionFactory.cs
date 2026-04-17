using System.Data;

namespace SyncAgent.Infrastructure.Database;

public interface ISqlConnectionFactory
{
    IDbConnection CreateOpenConnection();
}
