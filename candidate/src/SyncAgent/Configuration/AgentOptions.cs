using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Configuration;

/// <summary>
/// Settings governing the agent's polling loop and how much data a single task may return.
/// Validated at startup via <c>ValidateDataAnnotations().ValidateOnStart()</c>, so a bad value
/// fails the host immediately rather than surfacing as a runtime surprise on the first task.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>How long to wait before polling again after the queue reports 204 No Content.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "01:00:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Upper bound on rows returned by any single sync task. Without a cap, GetOrders alone
    /// returns 12,304 orders / 40,370 detail lines and produces a multi-megabyte POST body.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxRecords { get; init; } = 5000;

    /// <summary>Per-query SQL command timeout, in seconds.</summary>
    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}
