namespace TaskExecutionService.Configuration;

/// <summary>
/// Settings for the local AdventureWorks SQL Server connection. Bound from the
/// "Database" configuration section. The connection string may hold credentials
/// and should be supplied via user-secrets / environment variables in real
/// deployments rather than committed to source control.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>ADO.NET connection string for the AdventureWorks database.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Per-query command timeout in seconds (bounds long-running queries).</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
