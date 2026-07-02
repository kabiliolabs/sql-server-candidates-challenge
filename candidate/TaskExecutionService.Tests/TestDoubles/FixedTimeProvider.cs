namespace TaskExecutionService.Tests.TestDoubles;

/// <summary>A <see cref="TimeProvider"/> that always returns a fixed instant.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
