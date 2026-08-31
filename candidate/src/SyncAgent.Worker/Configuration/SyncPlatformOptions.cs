namespace SyncAgent.Worker.Configuration;

public class SyncPlatformOptions
{
    public const string SectionName = "SyncPlatform";

    public required string BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);
}
