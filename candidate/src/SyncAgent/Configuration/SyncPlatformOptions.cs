namespace SyncAgent.Configuration;

public sealed class SyncPlatformOptions
{
    public const string SectionName = "SyncPlatform";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public int PollingIntervalSeconds { get; init; } = 5;
}