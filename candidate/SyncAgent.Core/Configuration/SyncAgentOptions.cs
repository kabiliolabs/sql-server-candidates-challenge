namespace SyncAgent.Core.Configuration;

public class SyncAgentOptions
{
    public const string SectionName = "SyncAgent";

    public string ConnectionString { get; set; } = string.Empty;
    public string PlatformBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 10;
}
