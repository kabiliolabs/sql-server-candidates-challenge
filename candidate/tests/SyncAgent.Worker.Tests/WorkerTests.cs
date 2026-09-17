using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Dispatch;
using SyncAgent.Core.Tasks;
using Xunit;

namespace SyncAgent.Worker.Tests;

public class WorkerTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static IOptions<SyncAgentOptions> PollOptions() =>
        Options.Create(new SyncAgentOptions { PollIntervalSeconds = (int)PollInterval.TotalSeconds, MaxRowsPerTask = 5000 });

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
        return new SyncTaskDispatcher(provider.GetRequiredService<ISender>(), PollOptions());
    }

    /// <summary>Polls (real wall-clock, but with a short interval and generous timeout — this
    /// is only waiting for the Worker's background Task to process a FakeTimeProvider advance,
    /// not for the poll interval itself) until <paramref name="condition"/> is true or the
    /// timeout elapses. Keeps the tests deterministic about *how many* poll cycles happen
    /// (driven explicitly by advancing virtual time) without being sensitive to real scheduling
    /// jitter for *when* each cycle's async continuations actually run.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not met within the timeout.");
    }

    [Fact]
    public async Task ExecuteAsync_PlatformThrowsOnEveryPoll_LoopKeepsRunningInsteadOfCrashing()
    {
        var fakeTime = new FakeTimeProvider();
        var client = new FakeSyncPlatformClient(() => throw new HttpRequestException("platform unreachable"));
        var worker = new global::SyncAgent.Worker.Worker(client, CreateDispatcher(), PollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance, fakeTime);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await WaitUntilAsync(() => client.GetNextTaskCallCount >= 1, TimeSpan.FromSeconds(2));

        // If the exception had escaped the loop instead of being caught, this advance would
        // never produce a second poll — the background Task would already be faulted/completed.
        fakeTime.Advance(PollInterval);
        await WaitUntilAsync(() => client.GetNextTaskCallCount >= 2, TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, client.PostResultCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_NoTaskQueued_NeverPostsAResult()
    {
        var fakeTime = new FakeTimeProvider();
        var client = new FakeSyncPlatformClient(() => null);
        var worker = new global::SyncAgent.Worker.Worker(client, CreateDispatcher(), PollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance, fakeTime);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await WaitUntilAsync(() => client.GetNextTaskCallCount >= 1, TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

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
        var fakeTime = new FakeTimeProvider();
        var client = new FakeSyncPlatformClient(() =>
        {
            if (served)
            {
                return null;
            }

            served = true;
            return task;
        });
        var worker = new global::SyncAgent.Worker.Worker(client, CreateDispatcher(), PollOptions(), NullLogger<global::SyncAgent.Worker.Worker>.Instance, fakeTime);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await WaitUntilAsync(() => client.PostResultCallCount >= 1, TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, client.PostResultCallCount);
        Assert.Equal(SyncResultStatus.Failed, client.LastPostedResult?.Status);
        Assert.Equal("t1", client.LastPostedResult?.TaskId);
    }

    private sealed class EmptyRepository<TRecord> : ISyncQueryRepository<TRecord>
    {
        public Task<IReadOnlyList<TRecord>> GetModifiedSinceAsync(DateTimeOffset modifiedSince, int maxRows, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TRecord>>([]);
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
