using System;
using System.Globalization;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Validation
{
    /// <inheritdoc cref="ISyncTaskParameterReader" />
    public sealed class SyncTaskParameterReader : ISyncTaskParameterReader
    {
        public const string ModifiedSinceParameterName = "modifiedSince";

        /// <summary>
        /// Accepted spellings of the cutoff, in the order they are tried.
        /// </summary>
        /// <remarks>
        /// Parsing is exact rather than lenient on purpose. <c>DateTimeOffset.TryParse</c>
        /// would happily accept "01/02/2025" and silently pick a month, and a cutoff that is
        /// off by eleven months is a data bug nobody notices until reconciliation. The K
        /// specifier covers both the trailing Z the platform sends and an explicit offset;
        /// FFFFFFF makes the fractional part optional, so one format string handles both
        /// "2025-01-01T00:00:00Z" and "2025-02-07T10:01:36.827Z".
        /// </remarks>
        private static readonly string[] AcceptedFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            "yyyy-MM-dd",
        };

        private readonly SyncAgentOptions _options;
        private readonly ISystemClock _clock;

        public SyncTaskParameterReader(SyncAgentOptions options, ISystemClock clock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public DateTimeOffset ReadModifiedSince(SyncTaskRequest task)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (!task.Parameters.TryGetValue(ModifiedSinceParameterName, out var rawValue)
                || string.IsNullOrWhiteSpace(rawValue))
            {
                throw new SyncTaskValidationException(
                    "The task is missing the required 'modifiedSince' parameter.");
            }

            if (!DateTimeOffset.TryParseExact(
                    rawValue,
                    AcceptedFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var modifiedSince))
            {
                throw new SyncTaskValidationException(
                    "The 'modifiedSince' parameter is not a valid ISO-8601 timestamp.");
            }

            // Lower bound: a cutoff at DateTime.MinValue turns every incremental sync into a
            // full table scan of AdventureWorks. That is a cheap denial-of-service against
            // the client's production database, so it is refused rather than served.
            if (modifiedSince < _options.MinModifiedSince)
            {
                throw new SyncTaskValidationException(
                    "The 'modifiedSince' parameter is older than the configured minimum cutoff.");
            }

            // Upper bound: a future cutoff can only ever return nothing. Serving it would
            // report a successful sync of zero records and hide the real problem.
            var latestAcceptable = _clock.UtcNow + _options.ClockSkewTolerance;
            if (modifiedSince > latestAcceptable)
            {
                throw new SyncTaskValidationException(
                    "The 'modifiedSince' parameter is in the future.");
            }

            return modifiedSince;
        }
    }
}
