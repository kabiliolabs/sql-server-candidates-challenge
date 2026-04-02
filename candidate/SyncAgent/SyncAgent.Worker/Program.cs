using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Interfaces;
using SyncAgent.Infrastructure;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Handlers;
using SyncAgent.Infrastructure.Http;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection("Platform"))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Platform:BaseUrl must be a valid absolute URL.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "Platform:ApiKey is required.")
    .Validate(
        options => options.PollIntervalSeconds > 0,
        "Platform:PollIntervalSeconds must be greater than 0.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("AdventureWorks");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:AdventureWorks is required.");
}

// HTTP Client with API key handler
builder.Services.AddTransient<ApiKeyHandler>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
    return new ApiKeyHandler(options.ApiKey);
})
;

builder.Services.AddHttpClient<IPlatformClient, PlatformClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<ApiKeyHandler>();

// Repository
builder.Services.AddScoped<IAdventureWorksRepository>(_ => new AdventureWorksRepository(connectionString));

// Task handlers
builder.Services.AddScoped<ISyncTaskHandler, GetCustomersHandler>();
builder.Services.AddScoped<ISyncTaskHandler, GetProductsHandler>();
builder.Services.AddScoped<ISyncTaskHandler, GetOrdersHandler>();
builder.Services.AddScoped<ISyncTaskHandler, GetProductInventoryHandler>();

// Dispatcher
builder.Services.AddScoped<SyncDispatcher>();
builder.Services.AddScoped<ISyncTaskValidator, SyncTaskValidator>();

// Worker
builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
host.Run();
