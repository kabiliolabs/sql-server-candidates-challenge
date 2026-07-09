using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SyncAgent.Configuration;
using SyncAgent.Platform.Contracts;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests;

/// <summary>
/// The loop is driven by a <see cref="FakeTimeProvider"/>, so "waits one poll interval" is asserted
/// by refusing to advance the clock rather than by sleeping. Nothing here takes real wall-clock time
/// beyond the short spin in <see cref="WaitUntil"/>, which only ever waits for a background thread
/// to catch up.
/// </summary>
public class WorkerTests : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero));
    private readonly RecordingLogger<Worker> _logger = new();
    private readonly CancellationTokenSource _cts = new();

    private Worker CreateWorker(FakePlatformClient client, FakeSyncTaskDispatcher dispatcher) =>
        new(client, dispatcher, Options.Create(new AgentOptions { PollInterval = PollInterval }), _clock, _logger);

    private static SyncTask Task(string taskId) => new()
    {
        TaskId = taskId,
        TaskType = "GetCustomers",
        Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" },
    };

    /// <summary>Spins until <paramref name="condition"/> holds, optionally nudging the fake clock each turn.</summary>
    private static async Task WaitUntil(
        Func<bool> condition,
        Action? tick = null,
        [CallerArgumentExpression(nameof(condition))] string description = "")
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {description}");
            }

            tick?.Invoke();
            await System.Threading.Tasks.Task.Delay(5);
        }
    }

    // An idle agent must cost one request per interval, not a hot loop. Advancing the clock is the
    // only thing that lets the second poll happen, which is what proves the sleep is real.
    [Fact]
    public async Task An_empty_queue_makes_the_loop_wait_one_poll_interval()
    {
        var client = new FakePlatformClient();
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => client.GetNextTaskCallCount == 1);

        await System.Threading.Tasks.Task.Delay(50);
        client.GetNextTaskCallCount.Should().Be(1, "the loop must not poll again until the interval elapses");

        await WaitUntil(() => client.GetNextTaskCallCount >= 2, tick: () => _clock.Advance(PollInterval));
    }

    // A backlog drains at full speed: after a task is handled the loop polls again immediately.
    // The clock is never advanced here, so a sleep between tasks would hang the test.
    [Fact]
    public async Task A_queued_backlog_is_drained_without_waiting_between_tasks()
    {
        var client = new FakePlatformClient().Returns(Task("T1")).Returns(Task("T2")).Returns(Task("T3"));
        var dispatcher = new FakeSyncTaskDispatcher();
        var worker = CreateWorker(client, dispatcher);

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => client.PostedResults.Count == 3);

        dispatcher.DispatchedTasks.Select(t => t.TaskId).Should().Equal("T1", "T2", "T3");
        client.PostedResults.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_dequeued_task_is_dispatched_and_its_result_posted()
    {
        var client = new FakePlatformClient().Returns(Task("T1"));
        var expected = SyncResult.Failed("T1", "GetCustomers", "Unsupported task type: 'x'.", DateTime.UnixEpoch);
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher(_ => expected));

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => client.PostedResults.Count == 1);

        client.PostedResults.Single().Should().BeSameAs(expected);
    }

    // A null result means the task carried no id to report against. There is nothing to post,
    // and the platform would answer 400 if we tried.
    [Fact]
    public async Task Nothing_is_posted_when_the_dispatcher_has_no_result_to_report()
    {
        var client = new FakePlatformClient().Returns(Task("T1"));
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher(_ => null));

        await worker.StartAsync(_cts.Token);

        // The task counts as processed, so the loop polls again at once rather than sleeping.
        await WaitUntil(() => client.GetNextTaskCallCount >= 2);
        client.PostedResults.Should().BeEmpty();
    }

    // A client machine nobody is watching: the platform being unreachable is an expected condition,
    // not a reason to stop syncing forever.
    [Fact]
    public async Task A_failing_poll_is_logged_and_the_loop_keeps_going()
    {
        var client = new FakePlatformClient().Throws(new HttpRequestException("platform unreachable"));
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => _logger.Entries.Any(e => e.Level == LogLevel.Error));

        // It backs off for one interval rather than hammering an endpoint that is already unwell.
        await System.Threading.Tasks.Task.Delay(50);
        client.GetNextTaskCallCount.Should().Be(1);

        await WaitUntil(() => client.GetNextTaskCallCount >= 2, tick: () => _clock.Advance(PollInterval));
        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_result_the_platform_rejects_does_not_stop_the_loop()
    {
        var client = new FakePlatformClient().Returns(Task("T1"));
        client.ThrowOnPost = new HttpRequestException("400 Bad Request");
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => _logger.Entries.Any(e => e.Level == LogLevel.Error));
        await WaitUntil(() => client.GetNextTaskCallCount >= 2, tick: () => _clock.Advance(PollInterval));

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Shutdown_while_waiting_on_the_poll_interval_ends_the_loop_cleanly()
    {
        var client = new FakePlatformClient();
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => client.GetNextTaskCallCount == 1);

        await worker.StopAsync(CancellationToken.None);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        worker.ExecuteTask.IsFaulted.Should().BeFalse();
        _logger.Entries.Should().Contain(e => e.Message.Contains("Sync agent stopped", StringComparison.Ordinal));
    }

    // Shutdown that lands mid-poll, rather than while waiting on the interval. The cancellation
    // must end the loop quietly -- not be logged as a failed poll cycle, and not fault the host.
    [Fact]
    public async Task Shutdown_during_a_poll_ends_the_loop_without_reporting_an_error()
    {
        var client = new FakePlatformClient().Throws(() =>
        {
            _cts.Cancel();
            return new OperationCanceledException();
        });

        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await worker.StartAsync(_cts.Token);
        await WaitUntil(() => worker.ExecuteTask!.IsCompleted);

        worker.ExecuteTask!.IsFaulted.Should().BeFalse();
        _logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        _logger.Entries.Should().Contain(e => e.Message.Contains("Sync agent stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Shutdown_requested_before_the_first_poll_never_calls_the_platform()
    {
        var client = new FakePlatformClient();
        var worker = CreateWorker(client, new FakeSyncTaskDispatcher());

        await _cts.CancelAsync();
        await worker.StartAsync(_cts.Token);
        await worker.StopAsync(CancellationToken.None);

        client.GetNextTaskCallCount.Should().Be(0);
        worker.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_a_missing_dependency()
    {
        var client = new FakePlatformClient();
        var dispatcher = new FakeSyncTaskDispatcher();
        var options = Options.Create(new AgentOptions());

        FluentActions.Invoking(() => new Worker(null!, dispatcher, options, _clock, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new Worker(client, null!, options, _clock, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new Worker(client, dispatcher, null!, _clock, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new Worker(client, dispatcher, options, null!, _logger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new Worker(client, dispatcher, options, _clock, null!)).Should().Throw<ArgumentNullException>();
    }

    /// <summary>Cancels the linked token every worker was started with, so no loop outlives its test.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
