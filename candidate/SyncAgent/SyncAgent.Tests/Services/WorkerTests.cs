using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure;
using SyncAgent.Worker;
using Xunit;

namespace SyncAgent.Tests.Services;

public class WorkerTests
{
    private static IOptions<PlatformOptions> DefaultOptions =>
        Options.Create(new PlatformOptions { PollIntervalSeconds = 1 });

    private static SyncTaskValidator TaskValidator => new();

    private static SyncDispatcher MakeDispatcher(params ISyncTaskHandler[] handlers) =>
        new SyncDispatcher(handlers, NullLogger<SyncDispatcher>.Instance);

    [Fact]
    public async Task WhenNoTaskAvailable_DoesNotPostResult()
    {
        var platform = new Mock<IPlatformClient>();
        platform.Setup(p => p.GetNextTaskAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((SyncTask?)null);

        var worker = new SyncWorker(
            platform.Object,
            MakeDispatcher(),
            TaskValidator,
            DefaultOptions,
            NullLogger<SyncWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1100));
        await worker.StartAsync(cts.Token);
        await Task.Delay(1200);
        await worker.StopAsync(CancellationToken.None);

        platform.Verify(p => p.PostResultAsync(It.IsAny<SyncResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenKnownTask_HandlerIsCalledAndResultPosted()
    {
        var task = new SyncTask { TaskId = "t1", TaskType = "GetCustomers" };

        var platform = new Mock<IPlatformClient>();
        platform.SetupSequence(p => p.GetNextTaskAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ReturnsAsync((SyncTask?)null);

        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetCustomers");
        handler.Setup(h => h.HandleAsync(task, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SyncResult { TaskId = "t1", TaskType = "GetCustomers", Status = "completed", RecordCount = 5 });

        var worker = new SyncWorker(
            platform.Object,
            MakeDispatcher(handler.Object),
            TaskValidator,
            DefaultOptions,
            NullLogger<SyncWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        await worker.StartAsync(cts.Token);
        await Task.Delay(1600);
        await worker.StopAsync(CancellationToken.None);

        handler.Verify(h => h.HandleAsync(task, It.IsAny<CancellationToken>()), Times.Once);
        platform.Verify(p => p.PostResultAsync(
            It.Is<SyncResult>(r => r.Status == "completed" && r.RecordCount == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenUnknownTaskType_PostsFailedResult()
    {
        var task = new SyncTask { TaskId = "t2", TaskType = "UnknownType" };

        var platform = new Mock<IPlatformClient>();
        platform.SetupSequence(p => p.GetNextTaskAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ReturnsAsync((SyncTask?)null);

        var worker = new SyncWorker(
            platform.Object,
            MakeDispatcher(),
            TaskValidator,
            DefaultOptions,
            NullLogger<SyncWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        await worker.StartAsync(cts.Token);
        await Task.Delay(1600);
        await worker.StopAsync(CancellationToken.None);

        platform.Verify(p => p.PostResultAsync(
            It.Is<SyncResult>(r => r.Status == "failed" && r.TaskId == "t2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenHandlerThrows_PostsFailedResultAndContinues()
    {
        var task = new SyncTask { TaskId = "t3", TaskType = "GetProducts" };

        var platform = new Mock<IPlatformClient>();
        platform.SetupSequence(p => p.GetNextTaskAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(task)
                .ReturnsAsync((SyncTask?)null);

        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetProducts");
        handler.Setup(h => h.HandleAsync(It.IsAny<SyncTask>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new Exception("DB connection failed"));

        var worker = new SyncWorker(
            platform.Object,
            MakeDispatcher(handler.Object),
            TaskValidator,
            DefaultOptions,
            NullLogger<SyncWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        await worker.StartAsync(cts.Token);
        await Task.Delay(1600);
        await worker.StopAsync(CancellationToken.None);

        platform.Verify(p => p.PostResultAsync(
            It.Is<SyncResult>(r => r.Status == "failed" && r.ErrorMessage == "DB connection failed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenTaskHasInvalidModifiedSince_PostsFailedResultWithoutCallingHandler()
    {
        var task = new SyncTask
        {
            TaskId = "t4",
            TaskType = "GetCustomers",
            Parameters = new Dictionary<string, object?> { ["modifiedSince"] = "not-a-date" }
        };

        var platform = new Mock<IPlatformClient>();
        platform.SetupSequence(p => p.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(task)
            .ReturnsAsync((SyncTask?)null);

        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetCustomers");

        var worker = new SyncWorker(
            platform.Object,
            MakeDispatcher(handler.Object),
            TaskValidator,
            DefaultOptions,
            NullLogger<SyncWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        await worker.StartAsync(cts.Token);
        await Task.Delay(1600);
        await worker.StopAsync(CancellationToken.None);

        handler.Verify(h => h.HandleAsync(It.IsAny<SyncTask>(), It.IsAny<CancellationToken>()), Times.Never);
        platform.Verify(p => p.PostResultAsync(
            It.Is<SyncResult>(r =>
                r.Status == "failed" &&
                r.TaskId == "t4" &&
                r.ErrorMessage == "Invalid parameter: modifiedSince must be a valid date"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
