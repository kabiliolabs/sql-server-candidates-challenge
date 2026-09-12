using SyncAgent.Infrastructure;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

// No-op when started from a console, so the same build runs interactively and under the
// Windows Service Control Manager.
builder.Services.AddWindowsService(options => options.ServiceName = "SyncAgent");

builder.Services.AddInfrastructure(
    syncPlatform => builder.Configuration.GetSection(SyncPlatformOptions.SectionName).Bind(syncPlatform),
    database => builder.Configuration.GetSection(DatabaseOptions.SectionName).Bind(database),
    polling => builder.Configuration.GetSection(PollingOptions.SectionName).Bind(polling));

// Applies the option rules declared in AddInfrastructure at startup, so bad configuration stops
// the host immediately with a named setting rather than failing once polling begins.
builder.Services.AddOptions<SyncPlatformOptions>().ValidateOnStart();
builder.Services.AddOptions<DatabaseOptions>().ValidateOnStart();
builder.Services.AddOptions<PollingOptions>().ValidateOnStart();

builder.Services.AddHostedService<SyncAgentWorker>();

await builder.Build().RunAsync();
