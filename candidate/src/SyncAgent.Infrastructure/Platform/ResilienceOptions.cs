namespace SyncAgent.Infrastructure.Platform;

/// <summary>Tunables for the resilience pipeline wrapping every call to the platform
/// (both polling and result posting). All configurable via appsettings.json /
/// SyncAgent:Platform:Resilience — none of this is secret, so no env var needed.</summary>
public sealed class ResilienceOptions
{
    public int RetryMaxAttempts { get; init; } = 3;
    public double RetryBaseDelaySeconds { get; init; } = 1;

    /// <summary>Fraction of requests in the sampling window that must fail before the
    /// circuit opens (0.0-1.0).</summary>
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 30;
    public int CircuitBreakerMinimumThroughput { get; init; } = 4;
    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;

    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>Self-throttle: max outbound requests to the platform per second, across both
    /// polling and result posting — a defense against a misconfigured poll interval or a
    /// retry storm hammering the platform, independent of anything the platform itself does.</summary>
    public int MaxRequestsPerSecond { get; init; } = 10;
    public int RateLimiterQueueLimit { get; init; } = 5;
}
