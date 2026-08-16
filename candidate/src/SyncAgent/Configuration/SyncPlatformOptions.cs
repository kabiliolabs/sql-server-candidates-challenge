namespace SyncAgent.Configuration;

public sealed class SyncPlatformOptions
{
    public const string SectionName = "SyncPlatform";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public int PollingIntervalMSec { get; init; } = 1000;

    public int RequestTimeoutMsec { get; init; } = 30000;
}