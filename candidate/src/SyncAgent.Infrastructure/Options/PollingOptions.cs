namespace SyncAgent.Infrastructure.Options;

public class PollingOptions
{
    public const string SectionName = "Polling";

    public int IntervalSeconds { get; set; } = 10;
}
