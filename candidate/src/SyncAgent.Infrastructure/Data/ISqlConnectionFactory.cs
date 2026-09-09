using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SyncAgent.Infrastructure.Data
{
    /// <summary>
    /// Hands out open connections to the client database.
    /// </summary>
    /// <remarks>
    /// Queries never see the connection string, so the credential lives in exactly one place
    /// and cannot end up in a log line or an exception message from somewhere else.
    /// </remarks>
    public interface ISqlConnectionFactory
    {
        /// <summary>
        /// Opens a connection, or throws. The caller owns disposal.
        /// </summary>
        Task<SqlConnection> OpenAsync(CancellationToken cancellationToken);
    }
}
