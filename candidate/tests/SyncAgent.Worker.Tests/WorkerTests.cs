using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Tasks;
using Xunit;

namespace SyncAgent.Worker.Tests;

public class WorkerTests
{
    private static IOptions<SyncAgentOptions> FastPollOptions() =>
        Options.Create(new SyncAgentOptions { PollIntervalSeconds = 1, MaxRowsPerTask = 5000 });

    // No task type is ever actually dispatched to a real handler in these tests — the
    // Worker-loop behaviors under test (keep polling after an error, never post without a
    // task, post "failed" for an unrecognized type) don't need one; SyncTaskDispatcherTests
    // covers successful dispatch through a real Mediator pipeline. These empty repositories
    // exist only because AddMediator() auto-registers Core's real handlers and resolving
    // ISender eagerly builds its dispatch table, so every real handler's dependencies must be
    // satisfiable even though none of them get invoked here.
    private static SyncTaskDispatcher CreateDispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISyncQueryRepository<CustomerRecord>>(new EmptyRepository<CustomerRecord>());
        services.AddSingleton<ISyncQueryRepository<ProductRecord>>(new EmptyRepository<ProductRecord>());
        services.AddSingleton<ISyncQueryRepository<OrderRecord>>(new EmptyRepository<OrderRecord>());
        services.AddSingleton<ISyncQueryRepository<ProductInventoryRecord>>(new EmptyRepository<ProductInventoryRecord>());
        services.AddMediator();
        var provider = services.BuildServiceProvider();
        return new SyncTaskDispatcher(provider.GetRequiredService<ISender>(), FastPollOptions());
    }

    private sealed class EmptyRepository<TRecord> : ISyncQueryRepository<TRecord>
    {
        public Task<IReadOnlyList<TRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TRecord>>([]);
    }

    [Fact]
    public async Task ExecuteAsync_PlatformThrowsOnEveryPoll_LoopKeepsRunningInsteadOfCrashing()
    {
        var client = new FakeSyncPlatformClient(() => throw new HttpRequestException("platform unreachable"));
        var dispatcher = CreateDispatcher();
        var worker = new global::SyncAgent.Worker.Worker(client, dispatcher, FastPollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance);

        using var cts = new CancellationTokenSource();
        var executeTask = worker.StartAsync(cts.Token);

        // Let it fail a couple of poll cycles, then stop — if the loop had let the exception
        // escape, StartAsync/ExecuteAsync would have faulted instead of completing normally.
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(client.GetNextTaskCallCount >= 2, $"expected at least 2 poll attempts, got {client.GetNextTaskCallCount}");
        Assert.Equal(0, client.PostResultCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NoTaskQueued_NeverPostsAResult()
    {
        var client = new FakeSyncPlatformClient(() => null);
        var dispatcher = CreateDispatcher();
        var worker = new global::SyncAgent.Worker.Worker(client, dispatcher, FastPollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(client.GetNextTaskCallCount >= 1);
        Assert.Equal(0, client.PostResultCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_TaskWithUnknownType_StillPostsAFailedResult()
    {
        var task = new SyncTask
        {
            TaskId = "t1",
            TaskType = "SomeUnregisteredTaskType",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        var served = false;
        var client = new FakeSyncPlatformClient(() =>
        {
            if (served)
            {
                return null;
            }

            served = true;
            return task;
        });
        var dispatcher = CreateDispatcher();
        var worker = new global::SyncAgent.Worker.Worker(client, dispatcher, FastPollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, client.PostResultCallCount);
        Assert.Equal(SyncResultStatus.Failed, client.LastPostedResult?.Status);
        Assert.Equal("t1", client.LastPostedResult?.TaskId);
    }

    private sealed class FakeSyncPlatformClient(Func<SyncTask?> getNextTask) : ISyncPlatformClient
    {
        public int GetNextTaskCallCount { get; private set; }
        public int PostResultCallCount { get; private set; }
        public SyncResult? LastPostedResult { get; private set; }

        public Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
        {
            GetNextTaskCallCount++;
            return Task.FromResult(getNextTask());
        }

        public Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
        {
            PostResultCallCount++;
            LastPostedResult = result;
            return Task.CompletedTask;
        }
    }
}
