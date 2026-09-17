using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimiting;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Platform;

namespace SyncAgent.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything in Infrastructure: SQL options/connection factory, the
    /// four repositories, and the typed HttpClient for the platform. Connection string and API
    /// key are read from environment variables (SYNCAGENT_DB_CONNECTION, SYNCAGENT_API_KEY,
    /// SYNCAGENT_PLATFORM_URL) rather than appsettings.json, so secrets never sit in a
    /// committed file. Options are validated at startup (ValidateOnStart) — a missing or
    /// malformed value fails the host before it starts polling, not on the first task.</summary>
    public static IServiceCollection AddSyncAgentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Non-secret settings (CommandTimeoutSeconds) may come from appsettings.json; the
        // connection string never does — it's read from an environment variable only, so it
        // can never end up committed to source control by accident.
        services.AddOptions<SqlOptions>()
            .Bind(configuration.GetSection("SyncAgent:Sql"))
            .Configure(o => o.ConnectionString = Environment.GetEnvironmentVariable("SYNCAGENT_DB_CONNECTION") ?? o.ConnectionString)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlOptions>, SqlOptionsValidator>());
        services.AddSingleton<ISqlConnectionFactory>(sp =>
            new SqlConnectionFactory(sp.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString));

        services.AddSingleton<ISyncQueryRepository<CustomerRecord>, CustomerRepository>();
        services.AddSingleton<ISyncQueryRepository<ProductRecord>, ProductRepository>();
        services.AddSingleton<ISyncQueryRepository<OrderRecord>, OrderRepository>();
        services.AddSingleton<ISyncQueryRepository<ProductInventoryRecord>, ProductInventoryRepository>();

        services.AddOptions<PlatformOptions>()
            .Bind(configuration.GetSection("SyncAgent:Platform"))
            .Configure(o => o.ApiKey = Environment.GetEnvironmentVariable("SYNCAGENT_API_KEY") ?? o.ApiKey)
            .Configure(o => o.BaseUrl = Environment.GetEnvironmentVariable("SYNCAGENT_PLATFORM_URL") ?? o.BaseUrl)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PlatformOptions>, PlatformOptionsValidator>());

        services.AddOptions<ResilienceOptions>()
            .Bind(configuration.GetSection("SyncAgent:Platform:Resilience"))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ResilienceOptions>, ResilienceOptionsValidator>());

        services.AddTransient<ApiKeyDelegatingHandler>();
        services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((sp, client) =>
            {
                var platformOptions = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
                client.BaseAddress = new Uri(platformOptions.BaseUrl);
            })
            .AddHttpMessageHandler<ApiKeyDelegatingHandler>()
            .AddResilienceHandler("sync-platform", (builder, context) =>
            {
                var resilience = context.ServiceProvider.GetRequiredService<IOptions<ResilienceOptions>>().Value;

                // Order mirrors Microsoft's own standard resilience handler: rate limiter
                // outermost (self-throttle before spending any time), then timeout/retry/circuit
                // breaker guarding each attempt.
                var rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = resilience.MaxRequestsPerSecond,
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = resilience.RateLimiterQueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
                // AddRateLimiter(RateLimiter) treats an externally-supplied limiter as
                // caller-owned and never disposes it. HttpClientFactory rebuilds this handler
                // (and re-runs this whole callback) roughly every 2 minutes by default for a
                // long-running process, so without this, each rotation would leak the previous
                // limiter's internal replenishment timer.
                context.OnPipelineDisposed(rateLimiter.Dispose);
                builder.AddRateLimiter(rateLimiter);

                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = resilience.RetryMaxAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(resilience.RetryBaseDelaySeconds),
                    UseJitter = true
                });

                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = resilience.CircuitBreakerFailureRatio,
                    SamplingDuration = TimeSpan.FromSeconds(resilience.CircuitBreakerSamplingDurationSeconds),
                    MinimumThroughput = resilience.CircuitBreakerMinimumThroughput,
                    BreakDuration = TimeSpan.FromSeconds(resilience.CircuitBreakerBreakDurationSeconds)
                });

                builder.AddTimeout(TimeSpan.FromSeconds(resilience.TimeoutSeconds));
            });

        return services;
    }
}
