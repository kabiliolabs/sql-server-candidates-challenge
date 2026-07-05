namespace TaskExecutionService.Configuration;

/// <summary>
/// Settings for talking to the central sync platform. Bound from the "Platform"
/// configuration section. The <see cref="ApiKey"/> is a secret and should be
/// supplied via user-secrets or environment variables in real deployments.
/// </summary>
public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Base URL of the platform API, e.g. http://localhost:5100.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>API key sent in the X-Api-Key header on every request.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>How long to wait between polls when the queue is empty.</summary>
    public int PollingIntervalSeconds { get; set; } = 3;
}
