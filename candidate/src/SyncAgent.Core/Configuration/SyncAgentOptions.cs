using System;
using System.Collections.Generic;
using System.Globalization;

namespace SyncAgent.Core.Configuration
{
    /// <summary>
    /// Every knob the agent has, in one place.
    /// </summary>
    /// <remarks>
    /// The shape lives in Core and the values come from Infrastructure, so the domain never
    /// learns where configuration is stored. Defaults are chosen to be safe on an unattended
    /// client machine: bounded work, bounded memory, bounded retries.
    /// </remarks>
    public sealed class SyncAgentOptions
    {
        /// <summary>Base address of the platform, e.g. <c>http://localhost:5100</c>.</summary>
        public Uri? PlatformBaseUrl { get; set; }

        /// <summary>Value sent in the <c>X-Api-Key</c> header. Never logged in full.</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>SQL Server connection string for the client database.</summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>Delay between polls while the queue keeps producing work.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Ceiling for the idle backoff. Consecutive 204s double the wait up to this value so
        /// an agent left running overnight against an empty queue costs almost nothing.
        /// </summary>
        public TimeSpan MaxIdleBackoff { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Hard ceiling on rows returned per task, applied inside the SQL query.
        /// </summary>
        /// <remarks>
        /// This is a resource-exhaustion guard, not a paging feature. A task whose filter
        /// matches the whole table would otherwise materialise every row in memory and then
        /// serialise it into a single HTTP body.
        /// </remarks>
        public int MaxRecordsPerTask { get; set; } = 50_000;

        /// <summary>Serialised result bodies larger than this are refused before being sent.</summary>
        public int MaxResultPayloadBytes { get; set; } = 32 * 1024 * 1024;

        public TimeSpan SqlCommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>Total attempts for a transient failure, including the first one.</summary>
        public int MaxRetryAttempts { get; set; } = 4;

        /// <summary>First backoff step; each further attempt doubles it and adds jitter.</summary>
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Oldest cutoff a task may ask for. Guards against a <c>modifiedSince</c> of
        /// <c>0001-01-01</c>, which turns every incremental sync into a full table scan.
        /// </summary>
        public DateTimeOffset MinModifiedSince { get; set; } =
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// How far ahead of local time a platform-supplied timestamp may be before it is
        /// rejected. The platform and the client machine are different hosts with different
        /// clocks, so a few minutes of drift is normal.
        /// </summary>
        public TimeSpan ClockSkewTolerance { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Validates the whole object and throws once, listing every problem found.
        /// </summary>
        /// <remarks>
        /// Reporting all errors at once matters for an unattended service: an operator fixing
        /// a config file one restart per mistake is a bad afternoon.
        /// </remarks>
        public void Validate()
        {
            var errors = new List<string>();

            if (PlatformBaseUrl is null)
            {
                errors.Add("Platform base URL is missing.");
            }
            else if (!PlatformBaseUrl.IsAbsoluteUri)
            {
                errors.Add("Platform base URL must be an absolute URI.");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                errors.Add("Platform API key is missing.");
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                errors.Add("Database connection string is missing.");
            }

            if (PollInterval <= TimeSpan.Zero)
            {
                errors.Add("Poll interval must be greater than zero.");
            }

            if (MaxIdleBackoff < PollInterval)
            {
                errors.Add("Maximum idle backoff must be greater than or equal to the poll interval.");
            }

            if (MaxRecordsPerTask <= 0)
            {
                errors.Add("Maximum records per task must be greater than zero.");
            }

            if (MaxResultPayloadBytes <= 0)
            {
                errors.Add("Maximum result payload size must be greater than zero.");
            }

            if (SqlCommandTimeout <= TimeSpan.Zero)
            {
                errors.Add("SQL command timeout must be greater than zero.");
            }

            if (HttpTimeout <= TimeSpan.Zero)
            {
                errors.Add("HTTP timeout must be greater than zero.");
            }

            if (MaxRetryAttempts < 1)
            {
                errors.Add("Maximum retry attempts must be at least 1.");
            }

            if (RetryBaseDelay <= TimeSpan.Zero)
            {
                errors.Add("Retry base delay must be greater than zero.");
            }

            if (ClockSkewTolerance < TimeSpan.Zero)
            {
                errors.Add("Clock skew tolerance cannot be negative.");
            }

            if (errors.Count > 0)
            {
                throw new SyncAgentConfigurationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The sync agent configuration is invalid:{0}  - {1}",
                    Environment.NewLine,
                    string.Join(Environment.NewLine + "  - ", errors)));
            }
        }
    }
}
