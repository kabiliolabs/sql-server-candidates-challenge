using Microsoft.Extensions.Options;
using SyncAgent.Worker;
using SyncAgent.Worker.Configuration;
using SyncAgent.Worker.Data;
using SyncAgent.Worker.Platform;
using SyncAgent.Worker.Tasks;

var builder = Host.CreateApplicationBuilder(args);

// Registers the WindowsServiceLifetime so this same executable can be installed and
// managed as a real Windows Service via `sc create ... binPath=...`. When the process
// is *not* launched by the Service Control Manager (e.g. `dotnet run`, or non-Windows),
// this call is a no-op and the app behaves like a normal console app.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "SyncAgent";
});

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

builder.Services.Configure<SyncPlatformOptions>(
    builder.Configuration.GetSection(SyncPlatformOptions.SectionName));

builder.Services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SyncPlatformOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
});

builder.Services.AddSingleton<ISyncTaskHandler, GetCustomersTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductsTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetOrdersTaskHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductInventoryTaskHandler>();
builder.Services.AddSingleton<ISyncTaskDispatcher, SyncTaskDispatcher>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Fail fast if AdventureWorks isn't reachable at startup - a bad connection string or
// stopped SQL Server should surface immediately in the logs
await using (var scope = host.Services.CreateAsyncScope())
{
    var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await using var connection = await connectionFactory.CreateOpenConnectionAsync();
    logger.LogInformation(
        "Connected to AdventureWorks database (server version {ServerVersion}).",
        connection.ServerVersion);
}

host.Run();
