namespace SyncAgent.Configuration;

public class SyncAgentOptions
{
    public const string SectionName = "SyncAgent";

    public string PlatformBaseUrl { get; set; } = "http://localhost:5100";
    public string ApiKey { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
}
