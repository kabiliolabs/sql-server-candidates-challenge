using SyncTaskWorkerService;
using SyncTaskWorkerService.CentralPlatformHttpClient;
using SyncTaskWorkerService.Data;
using SyncTaskWorkerService.SyncAgentTask;
using SyncTaskWorkerService.SyncAgentTask.Handlers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IDbConnection>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString= configuration.GetConnectionString("SqlServer");
    return new SqlServerConnection(connectionString!);
});

builder.Services.AddHttpClient<IPlatformHttpClient, PlatformHttpClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HttpClient:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["HttpClient:ApiKey"]!);
});

builder.Services.AddSingleton<ISyncTaskHandler, GetCustomersHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetOrdersHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductsHandler>();
builder.Services.AddSingleton<ISyncTaskHandler, GetProductInventoryHandler>();


builder.Services.AddSingleton<SyncTaskDispatcher>();

builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
