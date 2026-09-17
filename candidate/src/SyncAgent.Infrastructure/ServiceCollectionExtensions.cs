using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Platform;

namespace SyncAgent.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything in Infrastructure: SQL options/connection factory, the
    /// four repositories, and the typed HttpClient for the platform. Connection string and API
    /// key are read from environment variables (SYNCAGENT_DB_CONNECTION, SYNCAGENT_API_KEY,
    /// SYNCAGENT_PLATFORM_URL) rather than appsettings.json, so secrets never sit in a
    /// committed file. Options are validated at startup (ValidateOnStart) — a missing or
    /// malformed value fails the host before it starts polling, not on the first task.</summary>
    public static IServiceCollection AddSyncAgentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Non-secret settings (CommandTimeoutSeconds) may come from appsettings.json; the
        // connection string never does — it's read from an environment variable only, so it
        // can never end up committed to source control by accident.
        services.AddOptions<SqlOptions>()
            .Bind(configuration.GetSection("SyncAgent:Sql"))
            .Configure(o => o.ConnectionString = Environment.GetEnvironmentVariable("SYNCAGENT_DB_CONNECTION") ?? o.ConnectionString)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlOptions>, SqlOptionsValidator>());
        services.AddSingleton<ISqlConnectionFactory>(sp =>
            new SqlConnectionFactory(sp.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString));

        services.AddSingleton<ISyncQueryRepository<CustomerRecord>, CustomerRepository>();
        services.AddSingleton<ISyncQueryRepository<ProductRecord>, ProductRepository>();
        services.AddSingleton<ISyncQueryRepository<OrderRecord>, OrderRepository>();
        services.AddSingleton<ISyncQueryRepository<ProductInventoryRecord>, ProductInventoryRepository>();

        services.AddOptions<PlatformOptions>()
            .Bind(configuration.GetSection("SyncAgent:Platform"))
            .Configure(o => o.ApiKey = Environment.GetEnvironmentVariable("SYNCAGENT_API_KEY") ?? o.ApiKey)
            .Configure(o => o.BaseUrl = Environment.GetEnvironmentVariable("SYNCAGENT_PLATFORM_URL") ?? o.BaseUrl)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PlatformOptions>, PlatformOptionsValidator>());

        services.AddTransient<ApiKeyDelegatingHandler>();
        services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((sp, client) =>
            {
                var platformOptions = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
                client.BaseAddress = new Uri(platformOptions.BaseUrl);
            })
            .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        return services;
    }
}
