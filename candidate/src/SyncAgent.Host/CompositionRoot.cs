using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncAgent.Core;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Handlers;
using SyncAgent.Core.Resilience;
using SyncAgent.Core.Time;
using SyncAgent.Core.Validation;
using SyncAgent.Infrastructure.Configuration;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Data.Queries;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Infrastructure.Resilience;

namespace SyncAgent.Host
{
    /// <summary>
    /// The one place in the application that knows every concrete type.
    /// </summary>
    /// <remarks>
    /// Core and Infrastructure reference only abstractions; the wiring lives here so that
    /// swapping an implementation — a different database, a stubbed platform for a smoke test
    /// — is a change to this file and nothing else.
    ///
    /// The handler block below is the answer to "how hard is it to add a task type?". It is
    /// one line, next to four identical ones.
    /// </remarks>
    public static class CompositionRoot
    {
        public static ServiceProvider Build(SyncAgentOptions options, ILoggerFactory loggerFactory)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var services = new ServiceCollection();

            services.AddSingleton(options);
            services.AddSingleton(loggerFactory);
            services.AddLogging();

            // --- Cross-cutting ---------------------------------------------------------
            services.AddSingleton<ISystemClock, SystemClock>();
            services.AddSingleton<IDelayProvider, TaskDelayProvider>();

            // --- Resilience ------------------------------------------------------------
            // Registered as an IEnumerable so a new dependency adds a detector rather than
            // editing a condition inside the retry policy.
            services.AddSingleton<ITransientErrorDetector, SqlTransientErrorDetector>();
            services.AddSingleton<ITransientErrorDetector, PlatformTransientErrorDetector>();
            services.AddSingleton<IRetryExecutor>(provider => new ExponentialBackoffRetryExecutor(
                provider.GetRequiredService<SyncAgentOptions>(),
                new CompositeTransientErrorDetector(provider.GetServices<ITransientErrorDetector>()),
                provider.GetRequiredService<IDelayProvider>(),
                provider.GetRequiredService<ILogger<ExponentialBackoffRetryExecutor>>()));

            // --- Platform --------------------------------------------------------------
            // One HttpClient for the process lifetime. See PlatformHttpClientFactory for why
            // that is not optional on .NET Framework.
            services.AddSingleton(_ => PlatformHttpClientFactory.Create(options));
            services.AddSingleton<ISyncPlatformClient>(provider => new SyncPlatformClient(
                provider.GetRequiredService<HttpClient>(),
                provider.GetRequiredService<SyncAgentOptions>(),
                provider.GetRequiredService<ILogger<SyncPlatformClient>>()));

            // --- Data ------------------------------------------------------------------
            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<ICustomerQuery, CustomerQuery>();
            services.AddSingleton<IProductQuery, ProductQuery>();
            services.AddSingleton<IOrderQuery, OrderQuery>();
            services.AddSingleton<IProductInventoryQuery, ProductInventoryQuery>();

            // --- Task handling ---------------------------------------------------------
            services.AddSingleton<ISyncTaskValidator, SyncTaskValidator>();
            services.AddSingleton<ISyncTaskParameterReader, SyncTaskParameterReader>();

            // Adding a new task type: write the handler, add one line here. Nothing else in
            // the application changes.
            services.AddSingleton<ISyncTaskHandler, GetCustomersHandler>();
            services.AddSingleton<ISyncTaskHandler, GetProductsHandler>();
            services.AddSingleton<ISyncTaskHandler, GetOrdersHandler>();
            services.AddSingleton<ISyncTaskHandler, GetProductInventoryHandler>();

            services.AddSingleton<ISyncTaskDispatcher, SyncTaskDispatcher>();

            services.AddSingleton<SyncAgentWorker>();

            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                // Catches a missing or ambiguous registration at startup rather than on the
                // first task that happens to need it.
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        /// <summary>
        /// Writes the effective configuration once at startup, with secrets redacted.
        /// </summary>
        /// <remarks>
        /// This is the line that answers "which database is it actually pointed at?" without
        /// anyone having to find the config file on a machine they cannot log into.
        /// </remarks>
        public static void LogEffectiveConfiguration(ILogger logger, SyncAgentOptions options)
        {
            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            logger.LogInformation(
                "Configuration: platform {BaseUrl}, api key {ApiKey}, database {ConnectionString}, "
                + "poll {PollInterval}, max idle backoff {MaxIdleBackoff}, max records {MaxRecords}, "
                + "sql timeout {SqlTimeout}, http timeout {HttpTimeout}, retries {Retries}.",
                options.PlatformBaseUrl,
                SecretRedactor.Redact(options.ApiKey),
                SecretRedactor.RedactConnectionString(options.ConnectionString),
                options.PollInterval,
                options.MaxIdleBackoff,
                options.MaxRecordsPerTask,
                options.SqlCommandTimeout,
                options.HttpTimeout,
                options.MaxRetryAttempts);
        }
    }
}
