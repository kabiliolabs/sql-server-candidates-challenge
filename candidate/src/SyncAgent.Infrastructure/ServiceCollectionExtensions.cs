using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using SyncAgent.Infrastructure.Http;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Infrastructure.Sql;

namespace SyncAgent.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<SyncPlatformOptions>? configureSyncPlatform = null,
        Action<DatabaseOptions>? configureDatabase = null,
        Action<PollingOptions>? configurePolling = null)
    {
        // Misconfiguration is rejected rather than surfacing later as a repeating failure on every
        // poll. The host calls ValidateOnStart, so these run once at startup.
        services.AddOptions<SyncPlatformOptions>()
            .Configure(options => configureSyncPlatform?.Invoke(options))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "SyncPlatform:BaseUrl must be an absolute URL, for example http://localhost:5100.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "SyncPlatform:ApiKey must not be empty.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "SyncPlatform:TimeoutSeconds must be greater than zero.");

        services.AddOptions<DatabaseOptions>()
            .Configure(options => configureDatabase?.Invoke(options))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Database:ConnectionString must not be empty.")
            .Validate(
                options => options.CommandTimeoutSeconds > 0,
                "Database:CommandTimeoutSeconds must be greater than zero.");

        services.AddOptions<PollingOptions>()
            .Configure(options => configurePolling?.Invoke(options))
            .Validate(
                options => options.IntervalSeconds > 0,
                "Polling:IntervalSeconds must be greater than zero.");

        services.AddHttpClient<SyncPlatformHttpClient>()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SyncPlatformOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            });

        services.AddScoped<ISyncPlatformClient>(sp => sp.GetRequiredService<SyncPlatformHttpClient>());

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        services.AddScoped<ISyncTaskHandler, GetCustomersHandler>();
        services.AddScoped<ISyncTaskHandler, GetProductsHandler>();
        services.AddScoped<ISyncTaskHandler, GetOrdersHandler>();
        services.AddScoped<ISyncTaskHandler, GetProductInventoryHandler>();

        services.AddScoped<ISyncTaskDispatcher, SyncTaskDispatcher>();
        services.AddScoped<ISyncTaskPoller, SyncTaskPoller>();

        // Singleton so connectivity state survives across poll cycles; the poller that reads it is
        // Scoped and rebuilt each cycle.
        services.AddSingleton<ISyncPlatformConnectivityTracker, SyncPlatformConnectivityTracker>();

        return services;
    }
}
