using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SyncAgent
{
    /// <summary>Windows Service host. OnStart validates config (fail fast), builds DI and
    /// starts the worker loop without blocking (SCM allows ~30s to report started).</summary>
    public sealed class SyncAgentService : ServiceBase
    {
        private ServiceProvider _provider;
        private SyncAgentWorker _worker;
        private CancellationTokenSource _cts;
        private Task _loopTask;
        private ILogger<SyncAgentService> _logger;

        public SyncAgentService()
        {
            ServiceName = "SyncAgent";
            CanStop = true;
            CanShutdown = true;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                AgentOptions options = Program.BuildOptions();
                _provider = Program.BuildServices(options, useConsole: false);
                _logger = _provider.GetRequiredService<ILogger<SyncAgentService>>();
                _cts = new CancellationTokenSource();
                _worker = _provider.GetRequiredService<SyncAgentWorker>();
                _loopTask = _worker.RunLoopAsync(_cts.Token);
                _logger.LogInformation("SyncAgent service started.");
            }
            catch (Exception ex)
            {
                Program.LogEventError("SyncAgent failed to start: " + ex.Message);
                throw; // fail fast — never sit "Running" while broken
            }
        }

        protected override void OnStop()
        {
            StopLoop();
            _logger?.LogInformation("SyncAgent service stopped.");
        }

        protected override void OnShutdown()
        {
            StopLoop();
        }

        private void StopLoop()
        {
            _cts?.Cancel();
            try
            {
                _loopTask?.Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException)
            {
                // cancelled task unwinds — nothing to surface at stop time
            }
            _provider?.Dispose();
            _provider = null;
        }
    }
}