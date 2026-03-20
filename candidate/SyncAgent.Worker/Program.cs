using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Interfaces;
using SyncAgent.Infrastructure.Handlers;
using SyncAgent.Infrastructure.Http;
using SyncAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration
builder.Services.Configure<SyncAgentOptions>(
    builder.Configuration.GetSection(SyncAgentOptions.SectionName));

// Register the API key handler as transient so HttpClientFactory can manage its lifetime
builder.Services.AddTransient<ApiKeyDelegatingHandler>();

// Typed HttpClient for the platform — base address and API key applied automatically
builder.Services
    .AddHttpClient<IPlatformClient, PlatformClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<SyncAgentOptions>>().Value;
        client.BaseAddress = new Uri(options.PlatformBaseUrl);
    })
    .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

// Register all task handlers — Worker resolves them as IEnumerable<ISyncTaskHandler>
// and builds a dictionary keyed by TaskType. Adding a new task = add one line here.
builder.Services.AddTransient<ISyncTaskHandler, GetCustomersHandler>();
builder.Services.AddTransient<ISyncTaskHandler, GetProductsHandler>();
builder.Services.AddTransient<ISyncTaskHandler, GetOrdersHandler>();
builder.Services.AddTransient<ISyncTaskHandler, GetProductInventoryHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
