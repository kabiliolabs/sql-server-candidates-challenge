using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace SyncAgent.Host
{
    /// <summary>
    /// Service metadata used by <c>SyncAgent.Host.exe install</c>.
    /// </summary>
    /// <remarks>
    /// The managed installer discovers this class by reflection over the assembly, which is
    /// why it must be public and carry <see cref="RunInstallerAttribute"/>. Shipping the
    /// installer inside the executable means deployment needs no extra tooling — no InstallUtil
    /// from the framework directory, no sc.exe command line to get subtly wrong.
    /// </remarks>
    [RunInstaller(true)]
    public sealed class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                // NetworkService rather than LocalService: it presents the machine account on
                // the network, which is what makes Integrated Security work against a SQL
                // Server on another host. LocalService authenticates as anonymous and would
                // force a SQL login with a password in the config file.
                Account = ServiceAccount.NetworkService,
            };

            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = SyncAgentService.WindowsServiceName,
                DisplayName = SyncAgentService.WindowsServiceDisplayName,
                Description = SyncAgentService.WindowsServiceDescription,
                StartType = ServiceStartMode.Automatic,

                // Delayed start keeps the agent out of the boot storm and, more importantly,
                // gives a SQL Server on the same machine time to finish recovery. Otherwise
                // every reboot begins with a burst of connection failures in the log.
                DelayedAutoStart = true,
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
