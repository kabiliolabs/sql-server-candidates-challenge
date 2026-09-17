using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Infrastructure;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

// log4net is the sole logging sink (default console/EventLog providers cleared) so every log
// line goes through the PII-safe config in log4net.config, on both console and rolling file.
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net("log4net.config");

builder.Services.AddSyncAgentCore(builder.Configuration);
builder.Services.AddSyncAgentInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

if (!TryValidateConfigurationOnStartup(host.Services, out var exitCode))
{
    return exitCode;
}

await host.RunAsync();
return 0;

// Fail fast: resolve every options type before the worker starts polling, so a missing or
// malformed connection string, API key, platform URL, or numeric setting is reported clearly
// and the process exits, instead of surfacing later as a confusing runtime error on the first
// task (or, worse, silently running with a null connection string).
static bool TryValidateConfigurationOnStartup(IServiceProvider services, out int exitCode)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var hasFailures = false;

    void Validate<TOptions>(string label) where TOptions : class
    {
        try
        {
            _ = services.GetRequiredService<IOptions<TOptions>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            hasFailures = true;
            foreach (var failure in ex.Failures)
            {
                logger.LogCritical("Configuration error ({Options}): {Failure}", label, failure);
            }
        }
    }

    Validate<SyncAgentOptions>(nameof(SyncAgentOptions));
    Validate<SqlOptions>(nameof(SqlOptions));
    Validate<PlatformOptions>(nameof(PlatformOptions));
    Validate<ResilienceOptions>(nameof(ResilienceOptions));

    if (hasFailures)
    {
        logger.LogCritical("Startup aborted due to invalid configuration. Check environment variables (SYNCAGENT_DB_CONNECTION, SYNCAGENT_API_KEY) and appsettings.json.");
    }

    exitCode = hasFailures ? 1 : 0;
    return !hasFailures;
}

public partial class Program;
