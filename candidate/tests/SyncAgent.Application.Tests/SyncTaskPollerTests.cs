using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using Xunit;

namespace SyncAgent.Application.Tests;

public class SyncTaskPollerTests
{
    private static SyncTaskPoller CreatePoller(
        Mock<ISyncPlatformClient> client,
        Mock<ISyncTaskDispatcher> dispatcher,
        ISyncPlatformConnectivityTracker? connectivityTracker = null)
    {
        return new SyncTaskPoller(
            client.Object,
            dispatcher.Object,
            connectivityTracker ?? new SyncPlatformConnectivityTracker(),
            NullLogger<SyncTaskPoller>.Instance);
    }

    private static HttpRequestException ConnectionRefused() =>
        new("Connection refused", new SocketException((int)SocketError.ConnectionRefused));

    [Fact]
    public async Task PollAndExecuteAsync_WhenPlatformIsUnreachable_CompletesWithoutThrowing()
    {
        // A refused TCP connection surfaces as HttpRequestException wrapping a SocketException.
        // The poller absorbs it so the hosted service's loop survives to the next cycle.
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ConnectionRefused());
        var dispatcher = new Mock<ISyncTaskDispatcher>();
        var poller = CreatePoller(client, dispatcher);

        var exception = await Record.ExceptionAsync(() => poller.PollAndExecuteAsync(CancellationToken.None));

        Assert.Null(exception);
        dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollAndExecuteAsync_WithRepeatedOutage_OnlyTheFirstPollMarksANewFailure()
    {
        // While an outage continues, every poll must not re-announce it: only the transition
        // into the outage counts as new.
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ConnectionRefused());
        var dispatcher = new Mock<ISyncTaskDispatcher>();
        var tracker = new SyncPlatformConnectivityTracker();
        var poller = CreatePoller(client, dispatcher, tracker);

        await poller.PollAndExecuteAsync(CancellationToken.None); // 1st failure: transitions reachable -> unreachable

        // The poller's own first call already consumed the "new failure" transition, so calling
        // RecordFailure() again here must report false - proving the state is now "known unreachable".
        Assert.False(tracker.RecordFailure());

        await poller.PollAndExecuteAsync(CancellationToken.None); // 2nd failure: still a repeat
        await poller.PollAndExecuteAsync(CancellationToken.None); // 3rd failure: still a repeat

        Assert.False(tracker.RecordFailure());
    }

    [Fact]
    public async Task PollAndExecuteAsync_WhenPlatformRecoversAfterAnOutage_LogsRestoredConnection()
    {
        var client = new Mock<ISyncPlatformClient>();
        var dispatcher = new Mock<ISyncTaskDispatcher>();
        var tracker = new SyncPlatformConnectivityTracker();
        tracker.RecordFailure(); // simulate a prior outage already recorded

        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ReturnsAsync((NextTaskResponse?)null);
        var poller = CreatePoller(client, dispatcher, tracker);

        await poller.PollAndExecuteAsync(CancellationToken.None);

        // The poller's successful call should have flipped the tracker back to "reachable",
        // so a fresh failure right after is reported as new again.
        Assert.True(tracker.RecordFailure());
    }

    [Fact]
    public async Task PollAndExecuteAsync_With204NoContent_DoesNotDispatchOrSubmit()
    {
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((NextTaskResponse?)null);
        var dispatcher = new Mock<ISyncTaskDispatcher>();
        var poller = CreatePoller(client, dispatcher);

        await poller.PollAndExecuteAsync(CancellationToken.None);

        dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        client.Verify(
            c => c.SubmitResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollAndExecuteAsync_WithSuccessfulTask_SubmitsCompletedStatus()
    {
        var task = new NextTaskResponse("task-1", "GetCustomers", new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" }, DateTime.UtcNow);
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ReturnsAsync(task);
        client
            .Setup(c => c.SubmitResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultSubmissionResponse(true, "task-1"));

        var dispatcher = new Mock<ISyncTaskDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync("GetCustomers", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SyncTaskExecutionResult.Completed(new object[3]));

        var poller = CreatePoller(client, dispatcher);

        await poller.PollAndExecuteAsync(CancellationToken.None);

        client.Verify(c => c.SubmitResultAsync(
            "task-1", "GetCustomers", "completed", It.IsAny<object?>(), 3, It.IsAny<DateTime>(), null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAndExecuteAsync_WithFailedHandler_SubmitsFailedStatusWithErrorMessage()
    {
        var task = new NextTaskResponse("task-2", "GetOrders", new Dictionary<string, string>(), DateTime.UtcNow);
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ReturnsAsync(task);
        client
            .Setup(c => c.SubmitResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultSubmissionResponse(true, "task-2"));

        var dispatcher = new Mock<ISyncTaskDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync("GetOrders", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SyncTaskExecutionResult.Failed("ModifiedSince parameter is required."));

        var poller = CreatePoller(client, dispatcher);

        await poller.PollAndExecuteAsync(CancellationToken.None);

        client.Verify(c => c.SubmitResultAsync(
            "task-2", "GetOrders", "failed", null, 0, It.IsAny<DateTime>(), "ModifiedSince parameter is required.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAndExecuteAsync_WithNullParametersFromPlatform_PassesEmptyDictionaryInsteadOfNull()
    {
        // System.Text.Json does not enforce non-nullable reference types, so a task can arrive with
        // "parameters" missing. Handlers must receive an empty dictionary, not null.
        var task = new NextTaskResponse("task-3", "GetProducts", null!, DateTime.UtcNow);
        var client = new Mock<ISyncPlatformClient>();
        client.Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>())).ReturnsAsync(task);
        client
            .Setup(c => c.SubmitResultAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultSubmissionResponse(true, "task-3"));

        Dictionary<string, string>? capturedParameters = null;
        var dispatcher = new Mock<ISyncTaskDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync("GetProducts", It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, string>, CancellationToken>((_, p, _) => capturedParameters = p)
            .ReturnsAsync(SyncTaskExecutionResult.Failed("ModifiedSince parameter is required."));

        var poller = CreatePoller(client, dispatcher);

        await poller.PollAndExecuteAsync(CancellationToken.None);

        Assert.NotNull(capturedParameters);
        Assert.Empty(capturedParameters!);
    }
}
