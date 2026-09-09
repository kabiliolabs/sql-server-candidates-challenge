using System;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Reads and validates the task parameters that handlers need.
    /// </summary>
    /// <remarks>
    /// Shared by all four handlers so the parsing rules for a given parameter are written
    /// once. A new task type reuses this instead of re-deriving how to read a cutoff date,
    /// which is where inconsistencies between sync jobs usually creep in.
    /// </remarks>
    public interface ISyncTaskParameterReader
    {
        /// <summary>
        /// Returns the <c>modifiedSince</c> cutoff, normalised to UTC.
        /// </summary>
        /// <exception cref="Exceptions.SyncTaskValidationException">
        /// The parameter is absent, is not an ISO-8601 timestamp, or falls outside the
        /// configured bounds.
        /// </exception>
        DateTimeOffset ReadModifiedSince(SyncTaskRequest task);
    }
}
