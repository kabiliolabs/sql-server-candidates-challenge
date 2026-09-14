namespace SyncAgent.Configuration;

/// <summary>
/// Configuration options for the Sync Agent: SyncPlatform API connectivity, database access, and polling behavior.
/// </summary>
public sealed class SyncAgentOptions
{
    public const string SectionName = "SyncAgent";

    /// <summary>
    /// Base URL of the SyncPlatform HTTP server (e.g. http://localhost:5100).
    /// </summary>
    public required string ApiBaseUrl { get; set; }

    /// <summary>
    /// API key sent via the X-Api-Key header on every request to the SyncPlatform server.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// SQL Server connection string for the AdventureWorks database.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Interval, in seconds, between polls of GET /api/sync/next-task.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 5;
}
