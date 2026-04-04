using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;
using SyncAgent.Worker;

namespace SyncAgent.Tests;

public class WorkerTests
{
    private readonly Mock<IPlatformClient> _platformClient = new();
    private readonly Mock<ISyncTaskHandler> _handler = new();
    private readonly SyncAgentOptions _options = new() { PollingIntervalSeconds = 10 };

    private Worker.Worker CreateWorker(params ISyncTaskHandler[] handlers)
    {
        return new Worker.Worker(
            _platformClient.Object,
            handlers,
            Options.Create(_options),
            NullLogger<Worker.Worker>.Instance);
    }

    [Fact]
    public async Task PollOnce_WhenNoTaskAvailable_DoesNotPostResult()
    {
        _platformClient
            .Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncTask?)null);

        var worker = CreateWorker();
        await worker.PollOnceAsync(CancellationToken.None);

        _platformClient.Verify(
            c => c.PostResultAsync(It.IsAny<SyncResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollOnce_WhenTaskReceived_ExecutesHandlerAndPostsSuccess()
    {
        var task = new SyncTask
        {
            TaskId = "task-1",
            TaskType = "GetProducts",
            Parameters = new TaskParameters()
        };

        var fakeData = new List<object> { new { Name = "Bike" } };

        _handler.Setup(h => h.TaskType).Returns("GetProducts");
        _handler
            .Setup(h => h.HandleAsync(task.Parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeData);

        _platformClient
            .Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var worker = CreateWorker(_handler.Object);
        await worker.PollOnceAsync(CancellationToken.None);

        _platformClient.Verify(
            c => c.PostResultAsync(
                It.Is<SyncResult>(r => r.TaskId == "task-1" && r.Status == SyncResultStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollOnce_WhenUnknownTaskType_PostsFailureResult()
    {
        var task = new SyncTask
        {
            TaskId = "task-2",
            TaskType = "DeleteEverything",
            Parameters = new TaskParameters()
        };

        _platformClient
            .Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var worker = CreateWorker(); // no handlers registered
        await worker.PollOnceAsync(CancellationToken.None);

        _platformClient.Verify(
            c => c.PostResultAsync(
                It.Is<SyncResult>(r => r.TaskId == "task-2" && r.Status == SyncResultStatus.Failed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollOnce_WhenHandlerThrows_PostsFailureResult()
    {
        var task = new SyncTask
        {
            TaskId = "task-3",
            TaskType = "GetCustomers",
            Parameters = new TaskParameters()
        };

        _handler.Setup(h => h.TaskType).Returns("GetCustomers");
        _handler
            .Setup(h => h.HandleAsync(It.IsAny<TaskParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        _platformClient
            .Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var worker = CreateWorker(_handler.Object);
        await worker.PollOnceAsync(CancellationToken.None);

        _platformClient.Verify(
            c => c.PostResultAsync(
                It.Is<SyncResult>(r => r.TaskId == "task-3" && r.Status == SyncResultStatus.Failed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
