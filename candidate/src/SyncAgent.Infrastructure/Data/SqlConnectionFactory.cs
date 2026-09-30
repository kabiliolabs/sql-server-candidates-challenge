using System.Data.Common;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Data
{
    public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
    {
        public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(options.Value.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }
}