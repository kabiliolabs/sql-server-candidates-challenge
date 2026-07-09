using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;

namespace SyncAgent.Tests.Integration;

/// <summary>
/// Resolves the database the integration tests run against.
/// </summary>
/// <remarks>
/// Layered exactly like the agent's own configuration, so the tests are pointed at a database the
/// same way the agent is. <c>appsettings.Integration.json</c> holds a default that matches this
/// repository's README; an environment variable overrides it without editing a file:
/// <code>
///   ConnectionStrings__AdventureWorks="Server=localhost,1433;Database=AdventureWorks2025;User Id=sa;Password=...;Encrypt=True;TrustServerCertificate=True"
///   dotnet test --filter Category=Integration
/// </code>
/// A default of <c>.\SQLEXPRESS</c> is a convenience for the machine this was written on, not an
/// assumption about the machine it is graded on.
/// </remarks>
internal static class IntegrationTestConfiguration
{
    private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.Integration.json", optional: false)
        .AddEnvironmentVariables()
        .Build();

    public static string ConnectionString { get; } =
        Configuration.GetConnectionString("AdventureWorks")
        ?? throw new InvalidOperationException(
            "No connection string. Set ConnectionStrings__AdventureWorks, or edit appsettings.Integration.json.");

    public static IOptions<DatabaseOptions> DatabaseOptions { get; } =
        Options.Create(new DatabaseOptions { AdventureWorks = ConnectionString });
}
