using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncAgent.Core;
using SyncAgent.Core.Configuration;
using SyncAgent.Infrastructure.Configuration;

namespace SyncAgent.Host
{
    /// <summary>
    /// Entry point. One executable, three modes: console, Windows Service, and installer.
    /// </summary>
    /// <remarks>
    /// A single binary that runs either way is the pattern that makes an always-on agent
    /// pleasant to work on. A reviewer presses F5 and watches it run; the client machine gets
    /// the same code under the service control manager, with no separate console build to
    /// drift out of sync with the deployed one.
    /// </remarks>
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitConfigurationError = 2;
        private const int ExitAlreadyRunning = 3;
        private const int ExitInstallError = 4;
        private const int ExitUnexpectedError = 5;

        private static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

            switch (command)
            {
                case "install":
                    return InstallService(uninstall: false);

                case "uninstall":
                    return InstallService(uninstall: true);

                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    return ExitSuccess;

                case "--console":
                case "-c":
                    return RunConsole();

                default:
                    // Environment.UserInteractive is false only when the process was started
                    // by the service control manager, so it is the right switch: the same
                    // command line does the right thing in both worlds.
                    return Environment.UserInteractive ? RunConsole() : RunService();
            }
        }

        private static int RunService()
        {
            ServiceBase.Run(new SyncAgentService());
            return ExitSuccess;
        }

        private static int RunConsole()
        {
            // Taken before any expensive setup: refusing early keeps a second instance from
            // opening connections or a log file it is about to be told to give up.
            using (var guard = SingleInstanceGuard.TryAcquire())
            {
                if (!guard.Acquired)
                {
                    Console.Error.WriteLine(
                        "Another sync agent is already running on this machine (service or console). "
                        + "Two agents would silently split the task queue between them.");
                    return ExitAlreadyRunning;
                }

                SyncAgentOptions options;

                try
                {
                    options = AppConfigOptionsProvider.Load();
                }
                catch (SyncAgentConfigurationException ex)
                {
                    // Configuration failures are printed plainly rather than logged: the
                    // logging stack is not up yet, and this is the one error a human is
                    // guaranteed to be watching for.
                    Console.Error.WriteLine(ex.Message);
                    return ExitConfigurationError;
                }

                var loggerFactory = LoggingSetup.Create(interactive: true);

                try
                {
                    using (var services = CompositionRoot.Build(options, loggerFactory))
                    using (var stopTokenSource = new CancellationTokenSource())
                    {
                        var logger = services.GetRequiredService<ILogger<SyncAgentWorker>>();
                        CompositionRoot.LogEffectiveConfiguration(logger, options);

                        Console.CancelKeyPress += (_, eventArgs) =>
                        {
                            // Cancel = true stops the runtime killing the process outright, so
                            // the in-flight task gets its shutdown grace period to report a
                            // result the platform will otherwise never hear about.
                            eventArgs.Cancel = true;
                            Console.WriteLine("Stopping; finishing the current task…");
                            stopTokenSource.Cancel();
                        };

                        var worker = services.GetRequiredService<SyncAgentWorker>();

                        worker.RunAsync(stopTokenSource.Token).GetAwaiter().GetResult();
                    }

                    return ExitSuccess;
                }
                catch (Exception ex)
                {
                    loggerFactory.CreateLogger("SyncAgent").LogCritical(ex, "The sync agent stopped unexpectedly.");
                    return ExitUnexpectedError;
                }
                finally
                {
                    LoggingSetup.Shutdown();
                }
            }
        }

        private static int InstallService(bool uninstall)
        {
            var executablePath = Assembly.GetExecutingAssembly().Location;

            try
            {
                var arguments = uninstall
                    ? new[] { "/u", executablePath }
                    : new[] { executablePath };

                ManagedInstallerClass.InstallHelper(arguments);

                Console.WriteLine(
                    uninstall
                        ? $"Service '{SyncAgentService.WindowsServiceName}' uninstalled."
                        : $"Service '{SyncAgentService.WindowsServiceName}' installed. Start it with: net start {SyncAgentService.WindowsServiceName}");

                return ExitSuccess;
            }
            catch (Exception ex)
            {
                // Almost always a missing elevation, and the framework's own message for that
                // is not obviously about permissions.
                Console.Error.WriteLine(
                    (uninstall ? "Uninstall" : "Install") + " failed: " + ex.Message);
                Console.Error.WriteLine("This command must be run from an elevated command prompt.");
                return ExitInstallError;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Kabilio SQL Server Sync Agent");
            Console.WriteLine();
            Console.WriteLine("  SyncAgent.Host.exe             Run as a console application, or as a");
            Console.WriteLine("                                 Windows service when started by the SCM.");
            Console.WriteLine("  SyncAgent.Host.exe --console   Force console mode.");
            Console.WriteLine("  SyncAgent.Host.exe install     Install the Windows service (elevated).");
            Console.WriteLine("  SyncAgent.Host.exe uninstall   Remove the Windows service (elevated).");
            Console.WriteLine();
            Console.WriteLine("Configuration lives in SyncAgent.Host.exe.config. The API key and");
            Console.WriteLine("connection string can be overridden with SYNCAGENT_PLATFORM_APIKEY and");
            Console.WriteLine("SYNCAGENT_CONNECTIONSTRING.");
        }
    }
}
