namespace SyncAgent.Core.Configuration;

/// <summary>Business/policy settings for the agent — as opposed to secrets or I/O endpoints
/// (connection string, platform URL, API key), which live in their own Infrastructure-level
/// options next to the code that consumes them. Bound via IOptions&lt;SyncAgentOptions&gt; and
/// validated at startup by <see cref="SyncAgentOptionsValidator"/> so a bad config value fails
/// fast instead of surfacing as a confusing runtime error later.</summary>
public sealed class SyncAgentOptions
{
    public const string SectionName = "SyncAgent";

    /// <summary>TOP(@n) row cap applied to every query — a defense against a hostile or
    /// malformed task payload, not real pagination (see Known Limitations).</summary>
    public int MaxRowsPerTask { get; init; } = 5000;

    public int PollIntervalSeconds { get; init; } = 5;
}
