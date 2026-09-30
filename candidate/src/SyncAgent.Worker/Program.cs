using SyncAgent.Infrastructure;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSyncAgent();
builder.Services.AddHostedService<PollingWorker>();

builder.Services.AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection(PlatformOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SyncOptions>()
    .Bind(builder.Configuration.GetSection(SyncOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseOptions>()
    .Configure<IConfiguration>((options, config) =>
        options.ConnectionString = config.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var host = builder.Build();
host.Run();