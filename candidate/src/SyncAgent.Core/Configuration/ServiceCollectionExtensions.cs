using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Handlers;

namespace SyncAgent.Core.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything in Core: options, the four task handlers, and the
    /// dispatcher. Adding a new task type means adding one handler class and one
    /// AddSingleton&lt;ISyncTaskHandler, ...&gt; line here — nothing else in this method changes.</summary>
    public static IServiceCollection AddSyncAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SyncAgentOptions>()
            .Bind(configuration.GetSection(SyncAgentOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Options.IValidateOptions<SyncAgentOptions>, SyncAgentOptionsValidator>());

        services.AddSingleton<ISyncTaskHandler, GetCustomersHandler>();
        services.AddSingleton<ISyncTaskHandler, GetProductsHandler>();
        services.AddSingleton<ISyncTaskHandler, GetOrdersHandler>();
        services.AddSingleton<ISyncTaskHandler, GetProductInventoryHandler>();

        services.AddSingleton<SyncTaskDispatcher>();

        return services;
    }
}
