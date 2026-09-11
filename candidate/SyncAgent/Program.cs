using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncAgent.Domain;
using SyncAgent.Handlers;
using SyncAgent.Protocol;
using SyncAgent.Sql;

namespace SyncAgent
{
    internal static class Program
    {
        private const string EventSource = "SyncAgent";

        private static void Main(string[] args)
        {
            bool console = args.Length > 0 && args[0] == "--console";
            if (console || Environment.UserInteractive)
                RunConsole();
            else
                ServiceBase.Run(new SyncAgentService());
        }

        private static void RunConsole()
        {
            AgentOptions options = BuildOptions();
            using (ServiceProvider provider = BuildServices(options, useConsole: true))
            using (var cts = new CancellationTokenSource())
            {
                ILogger log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
                var worker = provider.GetRequiredService<SyncAgentWorker>();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                log.LogInformation("SyncAgent starting in console mode (Ctrl+C to stop).");
                worker.RunLoopAsync(cts.Token).GetAwaiter().GetResult();
                log.LogInformation("SyncAgent stopped.");
            }
        }

        /// <summary>Reads appsettings.json + optional Local overrides and fails fast on bad values.</summary>
        public static AgentOptions BuildOptions()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Local.json", optional: true)
                .Build();

            var options = new AgentOptions();
            config.GetSection("Platform").Bind(options);
            options.ConnectionString = config.GetConnectionString("AdventureWorks");
            options.Validate();
            return options;
        }

        public static ServiceProvider BuildServices(AgentOptions options, bool useConsole)
        {
            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                if (useConsole)
                    builder.AddConsole();
                else
                    builder.AddEventLog(cfg =>
                    {
                        cfg.SourceName = EventSource;
                        cfg.LogName = "Application";
                    });
            });

            services.AddHttpClient<ISyncPlatformClient, SyncPlatformClient>((provider, client) =>
            {
                client.BaseAddress = new Uri(options.Url);
                client.Timeout = TimeSpan.FromSeconds(10); // cap stop/shutdown time on hung calls
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            });

            services.AddSingleton(options);
            services.AddSingleton(new SqlConnectionFactory(options.ConnectionString));
            services.AddSingleton<SyncTaskDispatcher>();
            services.AddSingleton<SyncAgentWorker>();
            services.AddTransient<ISyncTaskHandler, GetCustomersHandler>();
            services.AddTransient<ISyncTaskHandler, GetProductsHandler>();
            services.AddTransient<ISyncTaskHandler, GetOrdersHandler>();
            services.AddTransient<ISyncTaskHandler, GetProductInventoryHandler>();

            return services.BuildServiceProvider();
        }

        public static void LogEventError(string message)
        {
            try
            {
                if (!EventLog.SourceExists(EventSource))
                    EventLog.CreateEventSource(EventSource, "Application");
                EventLog.WriteEntry(EventSource, message, EventLogEntryType.Error);
            }
            catch
            {
                // best effort — the service install step creates the source
            }
        }
    }
}