using System;
using System.Data;
using Microsoft.Data.SqlClient;
using SyncAgent.Core.Configuration;

namespace SyncAgent.Infrastructure.Data
{
    /// <summary>
    /// Builds the command every sync query runs: same two parameters, same timeout.
    /// </summary>
    /// <remarks>
    /// Centralising this is a security control, not just deduplication. Every value that
    /// reaches SQL Server does so as a typed <see cref="SqlParameter"/>, and there is exactly
    /// one place in the codebase where a parameter is attached to a command. No query builds
    /// SQL by concatenation, and none of them can: the statement text is a compile-time
    /// constant in each query class and the only variables are bound here.
    /// </remarks>
    internal static class SqlCommandFactory
    {
        public const string MaxRecordsParameterName = "@maxRecords";
        public const string ModifiedSinceParameterName = "@modifiedSince";

        public static SqlCommand Create(
            string commandText,
            SqlConnection connection,
            SyncAgentOptions options,
            DateTimeOffset modifiedSince,
            int maxRecords)
        {
            var command = new SqlCommand(commandText, connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = (int)options.SqlCommandTimeout.TotalSeconds,
            };

            try
            {
                // SqlDbType.Int rather than letting the value infer its own type: TOP (@n)
                // requires an integer, and an inferred type turns a wrong value into a plan
                // compilation error at run time instead of a conversion error here.
                command.Parameters.Add(MaxRecordsParameterName, SqlDbType.Int).Value = maxRecords;

                // SqlDbType.DateTime, matching the column type. Binding a datetime2 parameter
                // against a datetime column forces an implicit conversion on every row of the
                // predicate, and the index seek on ModifiedDate is the only thing making an
                // incremental sync cheaper than a full scan.
                command.Parameters.Add(ModifiedSinceParameterName, SqlDbType.DateTime).Value =
                    modifiedSince.UtcDateTime;

                return command;
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }
    }
}
