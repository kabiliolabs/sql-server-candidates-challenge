using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure;
using Xunit;

namespace SyncAgent.Tests.Services;

public class DispatcherTests
{
    private static SyncDispatcher MakeDispatcher(params ISyncTaskHandler[] handlers) =>
        new(handlers, NullLogger<SyncDispatcher>.Instance);

    [Fact]
    public async Task Dispatcher_RoutesToCorrectHandler_ByTaskType()
    {
        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetCustomers");
        handler.Setup(h => h.HandleAsync(It.IsAny<SyncTask>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SyncResult { Status = "completed", RecordCount = 10 });

        var task = new SyncTask { TaskId = "1", TaskType = "GetCustomers" };
        var result = await MakeDispatcher(handler.Object).DispatchAsync(task);

        Assert.Equal("completed", result.Status);
        Assert.Equal(10, result.RecordCount);
        handler.Verify(h => h.HandleAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatcher_ReturnsFailed_ForUnknownTaskType()
    {
        var task = new SyncTask { TaskId = "2", TaskType = "UnknownType" };
        var result = await MakeDispatcher().DispatchAsync(task);

        Assert.Equal("failed", result.Status);
        Assert.Equal("2", result.TaskId);
        Assert.Contains("UnknownType", result.ErrorMessage);
    }

    [Fact]
    public async Task Dispatcher_ReturnsFailed_WhenHandlerThrows()
    {
        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetProducts");
        handler.Setup(h => h.HandleAsync(It.IsAny<SyncTask>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Connection lost"));

        var task = new SyncTask { TaskId = "3", TaskType = "GetProducts" };
        var result = await MakeDispatcher(handler.Object).DispatchAsync(task);

        Assert.Equal("failed", result.Status);
        Assert.Equal("Connection lost", result.ErrorMessage);
    }

    [Fact]
    public async Task Dispatcher_IsCaseInsensitive_ForTaskType()
    {
        var handler = new Mock<ISyncTaskHandler>();
        handler.Setup(h => h.TaskType).Returns("GetOrders");
        handler.Setup(h => h.HandleAsync(It.IsAny<SyncTask>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SyncResult { Status = "completed" });

        var task = new SyncTask { TaskId = "4", TaskType = "getorders" }; // lowercase
        var result = await MakeDispatcher(handler.Object).DispatchAsync(task);

        Assert.Equal("completed", result.Status);
    }
}
