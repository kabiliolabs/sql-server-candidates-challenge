namespace SyncAgent.Worker;

public class PlatformOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5100";
    public string ApiKey { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
}
