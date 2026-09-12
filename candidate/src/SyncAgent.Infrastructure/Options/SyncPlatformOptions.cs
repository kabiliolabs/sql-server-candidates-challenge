namespace SyncAgent.Infrastructure.Options;

public class SyncPlatformOptions
{
    public const string SectionName = "SyncPlatform";

    public string BaseUrl { get; set; } = "http://localhost:5100";
    public string ApiKey { get; set; } = "candidate-test-key-2026";
    public int TimeoutSeconds { get; set; } = 30;
}
