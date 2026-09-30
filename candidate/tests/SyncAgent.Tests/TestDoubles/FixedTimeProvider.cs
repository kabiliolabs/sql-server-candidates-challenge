namespace SyncAgent.Tests.TestDoubles;

/// <summary>Deterministic clock so date-range rules can be tested without depending on the real time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
