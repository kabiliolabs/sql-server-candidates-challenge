using System;
using System.Configuration;
using System.Globalization;
using SyncAgent.Core.Configuration;

namespace SyncAgent.Infrastructure.Configuration
{
    /// <summary>
    /// Builds <see cref="SyncAgentOptions"/> from App.config, with environment overrides.
    /// </summary>
    /// <remarks>
    /// App.config rather than appsettings.json because this is the .NET Framework idiom and,
    /// more usefully, because <c>connectionStrings</c> and <c>appSettings</c> are the two
    /// sections that <c>aspnet_regiis -pe</c> and
    /// <c>ConfigurationManager.ProtectSection</c> can encrypt at rest with DPAPI. On an
    /// unattended client machine that is a real control: the file on disk holds ciphertext
    /// that only the service account on that host can read, and no code change is needed to
    /// turn it on.
    ///
    /// Environment variables take precedence over the file for the two secrets. That is what
    /// lets a deployment supply credentials without writing them anywhere, and what keeps
    /// them out of the copy of App.config that ships in source control.
    /// </remarks>
    public static class AppConfigOptionsProvider
    {
        public const string ConnectionStringName = "AdventureWorks";

        public const string ApiKeyEnvironmentVariable = "SYNCAGENT_PLATFORM_APIKEY";
        public const string ConnectionStringEnvironmentVariable = "SYNCAGENT_CONNECTIONSTRING";

        /// <summary>
        /// Reads and validates the configuration. Throws rather than returning a partially
        /// usable object.
        /// </summary>
        public static SyncAgentOptions Load()
        {
            var options = new SyncAgentOptions
            {
                PlatformBaseUrl = ReadUri("Platform:BaseUrl"),
                ApiKey = ReadSecret(ApiKeyEnvironmentVariable, "Platform:ApiKey"),
                ConnectionString = ReadConnectionString(),
                PollInterval = ReadTimeSpanSeconds("Agent:PollIntervalSeconds", 5),
                MaxIdleBackoff = ReadTimeSpanSeconds("Agent:MaxIdleBackoffSeconds", 60),
                MaxRecordsPerTask = ReadInt32("Agent:MaxRecordsPerTask", 50_000),
                MaxResultPayloadBytes = ReadInt32("Agent:MaxResultPayloadBytes", 32 * 1024 * 1024),
                SqlCommandTimeout = ReadTimeSpanSeconds("Agent:SqlCommandTimeoutSeconds", 30),
                HttpTimeout = ReadTimeSpanSeconds("Agent:HttpTimeoutSeconds", 100),
                MaxRetryAttempts = ReadInt32("Agent:MaxRetryAttempts", 4),
                RetryBaseDelay = ReadTimeSpanSeconds("Agent:RetryBaseDelaySeconds", 1),
                MinModifiedSince = ReadDateTimeOffset("Agent:MinModifiedSince", new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                ClockSkewTolerance = ReadTimeSpanSeconds("Agent:ClockSkewToleranceSeconds", 300),
                ShutdownGracePeriod = ReadTimeSpanSeconds("Agent:ShutdownGracePeriodSeconds", 10),
            };

            options.Validate();

            return options;
        }

        private static string ReadConnectionString()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment!;
            }

            return ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString
                   ?? string.Empty;
        }

        private static string ReadSecret(string environmentVariable, string appSettingKey)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);

            return !string.IsNullOrWhiteSpace(fromEnvironment)
                ? fromEnvironment!
                : ConfigurationManager.AppSettings[appSettingKey] ?? string.Empty;
        }

        private static Uri? ReadUri(string key)
        {
            var raw = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                throw new SyncAgentConfigurationException(Invalid(key, "an absolute URL"));
            }

            return uri;
        }

        private static int ReadInt32(string key, int fallback)
        {
            var raw = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new SyncAgentConfigurationException(Invalid(key, "a whole number"));
            }

            return value;
        }

        private static TimeSpan ReadTimeSpanSeconds(string key, int fallbackSeconds)
        {
            return TimeSpan.FromSeconds(ReadInt32(key, fallbackSeconds));
        }

        private static DateTimeOffset ReadDateTimeOffset(string key, DateTimeOffset fallback)
        {
            var raw = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            if (!DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var value))
            {
                throw new SyncAgentConfigurationException(Invalid(key, "an ISO-8601 timestamp"));
            }

            return value;
        }

        /// <summary>
        /// Names the setting but never quotes its value: this message reaches the log, and
        /// the same code path reads the API key.
        /// </summary>
        private static string Invalid(string key, string expected)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Configuration setting '{0}' must be {1}.",
                key,
                expected);
        }
    }
}
