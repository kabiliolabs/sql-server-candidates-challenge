using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Infrastructure.Database;
using SyncAgent.Infrastructure.Http;
using SyncAgent.Services;
using SyncAgent.TaskHandlers;
using SyncAgent.Validation;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<SyncPlatformOptions>()
    .Bind(builder.Configuration.GetSection(SyncPlatformOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISyncTaskValidator, SyncTaskValidator>();
builder.Services.AddSingleton<ISyncTaskExecutor, SyncTaskExecutor>();
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IAdventureWorksReadRepository, AdventureWorksReadRepository>();

builder.Services.AddSingleton<ISyncTaskHandler, GetCustomersTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductsTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetOrdersTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductInventoryTaskHandler>();

builder.Services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SyncPlatformOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add(SyncPlatformClient.ApiKeyHeaderName, options.ApiKey);
});

builder.Services.AddHostedService<PollingBackgroundService>();

await builder.Build().RunAsync();