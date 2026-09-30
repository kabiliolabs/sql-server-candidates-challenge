using System.Data.Common;

namespace SyncAgent.Infrastructure.Data
{
    public interface IDbConnectionFactory
    {
        Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
    }
}