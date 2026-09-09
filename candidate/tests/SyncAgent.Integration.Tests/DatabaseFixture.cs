using System;
using SyncAgent.Core.Configuration;
using SyncAgent.Infrastructure.Data;
using Xunit;

namespace SyncAgent.Integration.Tests
{
    /// <summary>
    /// Where the integration tests get their database from.
    /// </summary>
    /// <remarks>
    /// Opt-in by environment variable rather than a connection string checked into the repo.
    /// A test suite that silently connects to whatever happens to be on localhost is a
    /// surprise nobody wants on a shared machine, and the reviewer's instance will not be
    /// named the same as mine.
    /// </remarks>
    public static class IntegrationTestEnvironment
    {
        public const string ConnectionStringVariable = "SYNCAGENT_TEST_CONNECTIONSTRING";

        public static string? ConnectionString =>
            Environment.GetEnvironmentVariable(ConnectionStringVariable);

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

        public static SyncAgentOptions Options()
        {
            return new SyncAgentOptions
            {
                PlatformBaseUrl = new Uri("http://localhost:5100/"),
                ApiKey = "not-used-by-these-tests",
                ConnectionString = ConnectionString ?? string.Empty,
                SqlCommandTimeout = TimeSpan.FromSeconds(30),
                MaxRecordsPerTask = 50_000,
            };
        }

        public static ISqlConnectionFactory ConnectionFactory() => new SqlConnectionFactory(Options());
    }

    /// <summary>
    /// A <see cref="FactAttribute"/> that skips itself when no test database is configured.
    /// </summary>
    /// <remarks>
    /// Skipping rather than failing keeps `dotnet test` green on a machine that has only the
    /// SDK — a reviewer should not have to restore AdventureWorks before they can run the
    /// suite. The skip reason names the variable so the choice is visible rather than silent.
    /// </remarks>
    public sealed class RequiresDatabaseFactAttribute : FactAttribute
    {
        public RequiresDatabaseFactAttribute()
        {
            if (!IntegrationTestEnvironment.IsConfigured)
            {
                Skip = $"Set {IntegrationTestEnvironment.ConnectionStringVariable} to run integration tests.";
            }
        }
    }
}
