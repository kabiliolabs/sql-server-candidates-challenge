using Microsoft.Extensions.Options;

namespace SyncAgent.Infrastructure.Platform;

public sealed class ResilienceOptionsValidator : IValidateOptions<ResilienceOptions>
{
    public ValidateOptionsResult Validate(string? name, ResilienceOptions options)
    {
        var failures = new List<string>();

        if (options.RetryMaxAttempts < 0)
        {
            failures.Add($"{nameof(ResilienceOptions.RetryMaxAttempts)} must be zero or greater.");
        }

        if (options.RetryBaseDelaySeconds <= 0)
        {
            failures.Add($"{nameof(ResilienceOptions.RetryBaseDelaySeconds)} must be greater than zero.");
        }

        if (options.CircuitBreakerFailureRatio is <= 0 or > 1)
        {
            failures.Add($"{nameof(ResilienceOptions.CircuitBreakerFailureRatio)} must be between 0 (exclusive) and 1 (inclusive).");
        }

        if (options.CircuitBreakerSamplingDurationSeconds <= 0)
        {
            failures.Add($"{nameof(ResilienceOptions.CircuitBreakerSamplingDurationSeconds)} must be greater than zero.");
        }

        if (options.CircuitBreakerMinimumThroughput < 2)
        {
            failures.Add($"{nameof(ResilienceOptions.CircuitBreakerMinimumThroughput)} must be at least 2.");
        }

        if (options.CircuitBreakerBreakDurationSeconds <= 0)
        {
            failures.Add($"{nameof(ResilienceOptions.CircuitBreakerBreakDurationSeconds)} must be greater than zero.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add($"{nameof(ResilienceOptions.TimeoutSeconds)} must be greater than zero.");
        }

        if (options.MaxRequestsPerSecond <= 0)
        {
            failures.Add($"{nameof(ResilienceOptions.MaxRequestsPerSecond)} must be greater than zero.");
        }

        if (options.RateLimiterQueueLimit < 0)
        {
            failures.Add($"{nameof(ResilienceOptions.RateLimiterQueueLimit)} must be zero or greater.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
