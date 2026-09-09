using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SyncAgent.Core.Abstractions;

namespace SyncAgent.Infrastructure.Resilience
{
    /// <summary>
    /// Recognises the SQL Server failures that are worth trying again.
    /// </summary>
    /// <remarks>
    /// The list is explicit rather than "retry any SqlException". A syntax error, a missing
    /// column or a permission denial are all SqlExceptions too, and retrying those four times
    /// with backoff turns a clear failure into a slow one that still fails.
    /// </remarks>
    public sealed class SqlTransientErrorDetector : ITransientErrorDetector
    {
        /// <summary>
        /// SQL Server error numbers that describe a temporary condition.
        /// </summary>
        private static readonly HashSet<int> TransientErrorNumbers = new HashSet<int>
        {
            -2,     // Command timeout expired
            20,     // Instance does not support encryption (handshake race during failover)
            64,     // Connection was successfully established but then broken
            233,    // No process on the other end of the pipe
            1205,   // Deadlock victim; the classic case for retrying an identical read
            10053,  // Transport-level error: connection aborted by the host
            10054,  // Transport-level error: connection reset by peer
            10060,  // Network or instance-specific error: connection timed out
            10928,  // Resource governor limit reached
            10929,  // Minimum guarantee not met because the server is too busy
            40197,  // Service encountered an error processing the request
            40501,  // Service is busy
            40613,  // Database not currently available
            49918,  // Cannot process request: not enough resources
            49919,  // Cannot process create or update request: too many operations
            49920,  // Cannot process request: too many operations in progress
            4060,   // Cannot open database requested by the login (seen during recovery)
        };

        public bool IsTransient(Exception exception)
        {
            switch (exception)
            {
                case SqlException sqlException:
                    // A single SqlException can carry several errors; any transient one in the
                    // collection means the command has a chance of succeeding on a retry.
                    foreach (SqlError error in sqlException.Errors)
                    {
                        if (TransientErrorNumbers.Contains(error.Number))
                        {
                            return true;
                        }
                    }

                    return false;

                case TimeoutException _:
                    return true;

                default:
                    return false;
            }
        }
    }
}
