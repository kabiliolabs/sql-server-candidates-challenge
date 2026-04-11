using Microsoft.Extensions.Options;
using SyncAgent;
using SyncAgent.Configuration;
using SyncAgent.Data;
using SyncAgent.Handlers;
using SyncAgent.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<SyncAgentOptions>(
    builder.Configuration.GetSection(SyncAgentOptions.SectionName));

// HTTP client with default API key header and base URL
builder.Services.AddHttpClient<IPlatformApiClient, PlatformApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SyncAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.PlatformBaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
});

// Data
builder.Services.AddSingleton<IAdventureWorksRepository, AdventureWorksRepository>();

// Task handlers — register each handler; adding a new task type = add one line here
builder.Services.AddSingleton<ITaskHandler, GetCustomersHandler>();
builder.Services.AddSingleton<ITaskHandler, GetProductsHandler>();
builder.Services.AddSingleton<ITaskHandler, GetOrdersHandler>();
builder.Services.AddSingleton<ITaskHandler, GetProductInventoryHandler>();

// Factory resolves handler by task type
builder.Services.AddSingleton<ITaskHandlerFactory, TaskHandlerFactory>();

// Hosted service (polling loop)
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
