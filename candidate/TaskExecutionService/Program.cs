using System.Net.Http.Headers;
using TaskExecutionService;
using TaskExecutionService.Configuration;
using TaskExecutionService.Data;
using TaskExecutionService.Platform;
using TaskExecutionService.Sync;
using TaskExecutionService.Sync.Executors;

var builder = Host.CreateApplicationBuilder(args);

// --- Options -------------------------------------------------------------
builder.Services
    .AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection(PlatformOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Platform:BaseUrl must be set.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Platform:ApiKey must be set (use user-secrets or env vars).")
    .ValidateOnStart();

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Database:ConnectionString must be set.")
    .ValidateOnStart();

// --- Data access ---------------------------------------------------------
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// --- Task executors (register one line per task type) --------------------
builder.Services.AddSingleton<ISyncTaskExecutor, GetCustomersExecutor>();
builder.Services.AddSingleton<ISyncTaskExecutor, GetProductsExecutor>();
builder.Services.AddSingleton<ISyncTaskExecutor, GetOrdersExecutor>();
builder.Services.AddSingleton<ISyncTaskExecutor, GetProductInventoryExecutor>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITaskDispatcher, TaskDispatcher>();

// --- Platform HTTP client (base address + API key configured here) -------
builder.Services.AddHttpClient<IPlatformClient, PlatformClient>((serviceProvider, http) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<PlatformOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
