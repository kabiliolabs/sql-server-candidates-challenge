using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SyncAgent.Core.Configuration;

namespace SyncAgent.Infrastructure.Data
{
    /// <inheritdoc cref="ISqlConnectionFactory" />
    public sealed class SqlConnectionFactory : ISqlConnectionFactory
    {
        /// <summary>
        /// Stamped onto every connection as the application name.
        /// </summary>
        /// <remarks>
        /// This shows up in sys.dm_exec_sessions and in Profiler traces. On a client machine
        /// where several applications share one SQL Server, it is the difference between a
        /// DBA being able to attribute a long-running query to this agent and having to guess.
        /// </remarks>
        public const string ApplicationName = "KabilioSyncAgent";

        private readonly string _connectionString;

        public SqlConnectionFactory(SyncAgentOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            try
            {
                // Parsing here rather than at first use turns a typo in the connection string
                // into a startup failure instead of a task failure an hour later.
                var builder = new SqlConnectionStringBuilder(options.ConnectionString)
                {
                    ApplicationName = ApplicationName,

                    // The agent reads one result set at a time and never interleaves readers
                    // on one connection, so MARS buys nothing and costs a slower protocol.
                    MultipleActiveResultSets = false,
                };

                _connectionString = builder.ConnectionString;
            }
            catch (ArgumentException ex)
            {
                // The exception from SqlConnectionStringBuilder can quote the offending
                // keyword, so it is replaced rather than wrapped: this message may reach a
                // log that is shipped off the machine.
                throw new SyncAgentConfigurationException(
                    "The database connection string is not valid.",
                    ex);
            }
        }

        public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);

            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                // Without this the failed connection leaks until finalisation, and under a
                // platform outage that is one leaked handle per poll.
                connection.Dispose();
                throw;
            }
        }
    }
}
