using System;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SyncAgent.Infrastructure.Configuration
{
    /// <summary>
    /// Renders secrets in a form that is safe to write to a log.
    /// </summary>
    /// <remarks>
    /// The agent logs its effective configuration at startup, which is the single most useful
    /// line in the file when something is misconfigured — and the single most dangerous if it
    /// is printed verbatim. Agent logs get pasted into tickets and shipped to central
    /// collectors, so the API key and the connection string are reduced to just enough to
    /// tell two environments apart.
    /// </remarks>
    public static class SecretRedactor
    {
        private const int VisiblePrefixLength = 4;

        /// <summary>
        /// Keeps the first few characters and the length, hides the rest.
        /// </summary>
        /// <remarks>
        /// Enough to confirm which key is loaded and to see that it is not empty or truncated,
        /// not enough to use. The length is safe to show and is often the actual diagnosis —
        /// a key with a trailing newline from a copy-paste is otherwise invisible.
        /// </remarks>
        public static string Redact(string? secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return "(not set)";
            }

            var visible = secret!.Length <= VisiblePrefixLength
                ? string.Empty
                : secret.Substring(0, VisiblePrefixLength);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}… ({1} chars)",
                visible,
                secret.Length);
        }

        /// <summary>
        /// Rebuilds a connection string with only the parts that identify the target.
        /// </summary>
        /// <remarks>
        /// Parsed and reassembled rather than pattern-matched: a regex over "Password=" misses
        /// the "Pwd" alias, misses the escaping rules for a password containing a semicolon,
        /// and fails open. Building a new string from an allow-list of keywords cannot leak a
        /// keyword nobody thought of.
        /// </remarks>
        public static string RedactConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return "(not set)";
            }

            try
            {
                var source = new SqlConnectionStringBuilder(connectionString);

                var safe = new SqlConnectionStringBuilder
                {
                    DataSource = source.DataSource,
                    InitialCatalog = source.InitialCatalog,
                    IntegratedSecurity = source.IntegratedSecurity,
                    ApplicationName = source.ApplicationName,
                };

                if (!source.IntegratedSecurity && !string.IsNullOrEmpty(source.UserID))
                {
                    safe.UserID = source.UserID;
                }

                return safe.ConnectionString;
            }
            catch (ArgumentException)
            {
                // Unparseable, so nothing can be safely extracted from it.
                return "(unparseable)";
            }
        }
    }
}
