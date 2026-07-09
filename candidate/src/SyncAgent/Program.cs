using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyncAgent;
using SyncAgent.Configuration;
using SyncAgent.Platform;
using SyncAgent.Sync;
using SyncAgent.Validation;

var builder = Host.CreateApplicationBuilder(args);

// Environment variables are layered over the JSON files by the default host configuration,
// so `Platform__ApiKey` overrides the development placeholder without touching a file.
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection(PlatformOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IPlatformClient, PlatformClient>()
    // Retry with exponential backoff and jitter, a concurrency limiter and a circuit breaker.
    // A client-side agent polling a platform over a flaky on-premise link needs all three:
    // without the breaker, an outage turns every poll into a retry storm.
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);

        // Polly requires the breaker's sampling window to be at least twice the attempt timeout.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });

// Injected rather than called statically, so the polling loop can be tested against a fake clock
// instead of sleeping in real time. Nothing in the agent calls DateTime.UtcNow.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<ITaskValidator, TaskValidator>();
builder.Services.AddSingleton<ISyncTaskDispatcher, SyncTaskDispatcher>();

// Adding a fifth task type means one class implementing ISyncTaskHandler and one registration
// line here. The dispatcher builds its routing table from whatever is registered.

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
