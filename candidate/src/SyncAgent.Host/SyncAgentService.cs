using System;
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
    /// Windows Service wrapper around <see cref="SyncAgentWorker"/>.
    /// </summary>
    /// <remarks>
    /// The service control manager gives OnStart a limited window — 30 seconds by default —
    /// before it declares the start hung, so OnStart only kicks off the loop and returns. All
    /// the actual work happens on the returned task.
    /// </remarks>
    public sealed class SyncAgentService : ServiceBase
    {
        public const string WindowsServiceName = "KabilioSyncAgent";
        public const string WindowsServiceDisplayName = "Kabilio SQL Server Sync Agent";
        public const string WindowsServiceDescription =
            "Polls the Kabilio platform for sync tasks, runs them against the local SQL Server "
            + "database and reports the results.";

        private readonly CancellationTokenSource _stopTokenSource = new CancellationTokenSource();

        private ServiceProvider? _services;
        private Task? _workerTask;
        private ILogger<SyncAgentService>? _logger;

        public SyncAgentService()
        {
            ServiceName = WindowsServiceName;

            // The SCM sends a stop on shutdown as well as on `net stop`, and without this the
            // agent is simply killed mid-task when the machine reboots.
            CanShutdown = true;
            CanStop = true;
            AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            var options = AppConfigOptionsProvider.Load();
            var loggerFactory = LoggingSetup.Create(interactive: false);

            _services = CompositionRoot.Build(options, loggerFactory);
            _logger = _services.GetRequiredService<ILogger<SyncAgentService>>();

            CompositionRoot.LogEffectiveConfiguration(_logger, options);

            var worker = _services.GetRequiredService<SyncAgentWorker>();

            _workerTask = Task.Run(
                () => RunGuardedAsync(worker, _stopTokenSource.Token),
                CancellationToken.None);
        }

        protected override void OnStop()
        {
            StopWorker();
        }

        protected override void OnShutdown()
        {
            StopWorker();
        }

        private async Task RunGuardedAsync(SyncAgentWorker worker, CancellationToken cancellationToken)
        {
            try
            {
                await worker.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // An exception escaping the loop leaves the service reporting Running while
                // doing nothing at all, which is the worst possible failure mode for an
                // unattended agent. Log it and stop, so the SCM's recovery options can
                // restart the service.
                _logger?.LogCritical(ex, "The sync agent stopped unexpectedly.");
                Stop();
            }
        }

        private void StopWorker()
        {
            _logger?.LogInformation("Stop requested; finishing the current task.");

            _stopTokenSource.Cancel();

            try
            {
                // Bounded wait. The worker's own shutdown grace period is shorter than this,
                // so a clean stop always wins the race; this only exists so a wedged task
                // cannot hold the SCM past its timeout.
                _workerTask?.Wait(TimeSpan.FromSeconds(20));
            }
            catch (AggregateException)
            {
                // Already logged by RunGuardedAsync.
            }

            _services?.Dispose();
            LoggingSetup.Shutdown();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stopTokenSource.Dispose();
                _services?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
