using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SyncAgent.Application.Services;
using SyncAgent.Infrastructure.Options;
using Xunit;

namespace SyncAgent.Worker.Tests;

public class SyncAgentWorkerTests
{
    [Fact]
    public async Task StartAsync_ThenStopAsync_ShutsDownGracefullyWithoutThrowing()
    {
        // A long polling interval keeps the PeriodicTimer from ever ticking during this test,
        // so this exercises graceful shutdown itself rather than depending on poll timing.
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<PollingOptions>(o => o.IntervalSeconds = 3600);
        services.AddScoped<ISyncTaskPoller, NeverCalledPoller>();
        await using var provider = services.BuildServiceProvider();

        var worker = new SyncAgentWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<PollingOptions>>(),
            NullLogger<SyncAgentWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        var exception = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    private sealed class NeverCalledPoller : ISyncTaskPoller
    {
        public Task PollAndExecuteAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The polling interval is 1 hour; this must not run during the test.");
        }
    }
}
