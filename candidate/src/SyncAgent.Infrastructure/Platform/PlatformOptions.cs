namespace SyncAgent.Infrastructure.Platform;

public sealed class PlatformOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Read from the SYNCAGENT_API_KEY environment variable — never committed to
    /// source control.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
