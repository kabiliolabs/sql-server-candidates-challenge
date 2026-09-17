using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Dispatch;

namespace SyncAgent.Core.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything in Core: options, Mediator (which source-generator-discovers
    /// the four IRequestHandler&lt;TQuery, TResponse&gt; implementations in this assembly), the
    /// logging pipeline behavior, and the dispatcher. Adding a new task type means one new
    /// IRequest record, one new IRequestHandler class, and one line in
    /// SyncTaskDispatcher.QuerySenders — nothing else in this method changes.</summary>
    public static IServiceCollection AddSyncAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SyncAgentOptions>()
            .Bind(configuration.GetSection(SyncAgentOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SyncAgentOptions>, SyncAgentOptionsValidator>());

        services.AddMediator();
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));

        services.AddSingleton<SyncTaskDispatcher>();

        return services;
    }
}
