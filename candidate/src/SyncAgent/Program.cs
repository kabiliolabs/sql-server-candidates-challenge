using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using SyncAgent;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Repository;
using AdventureWorks.Ef.Data;
using SyncAgent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

builder.Services.AddOptions<SyncPlatformOptions>()
    .Bind(builder.Configuration.GetSection(SyncPlatformOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl,UriKind.Absolute, out _), "SyncPlatform BaseUrl must be a valid absolute URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "SyncPlatform ApiKey is required.")
    .Validate(options => options.PollingIntervalMSec > 0, "PollingIntervalMSec must be greater than zero.")
    .Validate(options => options.RequestTimeoutMsec > 0, "RequestTimeoutMsec must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SyncPlatformOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    client.Timeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMsec);
});

builder.Services.AddDbContext<AdventureWorksDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AdventureWorks") ?? throw new InvalidOperationException(
                "AdventureWorks connection string is missing."),
        sqlOptions => sqlOptions.UseHierarchyId()));

builder.Services.AddScoped<ISyncTaskDispatcher, SyncTaskDispatcher>();
builder.Services.AddScoped<IAdventureWorksRepository, AdventureWorksRepository>();

builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();

host.Run();