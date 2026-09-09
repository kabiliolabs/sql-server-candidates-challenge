using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace SyncAgent.Host
{
    /// <summary>
    /// Configures logging: console for interactive runs, a rolling file always.
    /// </summary>
    /// <remarks>
    /// The application logs through <see cref="ILogger{TCategoryName}"/> everywhere; Serilog
    /// is only the sink behind it. That keeps the logging library out of Core and
    /// Infrastructure entirely and makes it replaceable from this one file.
    ///
    /// The file sink is what makes the Windows Service mode usable at all: a service has no
    /// console, so without it the first sixteen hours of an unattended agent's life would be
    /// unobservable.
    /// </remarks>
    public static class LoggingSetup
    {
        private const string ConsoleTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        private const string FileTemplate =
            "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

        public static ILoggerFactory Create(bool interactive)
        {
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.File(
                    path: Path.Combine(LogDirectory, "sync-agent-.log"),
                    rollingInterval: RollingInterval.Day,

                    // Bounded on both axes. An unattended agent runs for months, and a log
                    // that grows without limit eventually fills the client's system drive,
                    // which is a far worse outage than the one it was meant to help diagnose.
                    retainedFileCountLimit: 14,
                    fileSizeLimitBytes: 32L * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: FileTemplate);

            if (interactive)
            {
                configuration = configuration.WriteTo.Console(outputTemplate: ConsoleTemplate);
            }

            Log.Logger = configuration.CreateLogger();

            return new SerilogLoggerFactory(Log.Logger, dispose: true);
        }

        /// <summary>
        /// Logs live next to the executable.
        /// </summary>
        /// <remarks>
        /// Deliberately not %TEMP% or the user profile: as a Windows service this process runs
        /// as NetworkService, whose profile is somewhere nobody thinks to look. Beside the
        /// binary is where an engineer troubleshooting a client machine will actually go.
        /// </remarks>
        private static string LogDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }
    }
}
