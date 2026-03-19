using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Configuration;

public sealed class SyncPlatformOptions
{
    public const string SectionName = "SyncPlatform";

    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 5;

    [Range(1, 300)]
    public int HttpTimeoutSeconds { get; init; } = 30;
}
