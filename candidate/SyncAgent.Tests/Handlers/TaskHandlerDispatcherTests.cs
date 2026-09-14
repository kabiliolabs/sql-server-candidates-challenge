using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Contracts;
using SyncAgent.Handlers;

namespace SyncAgent.Tests.Handlers;

public class TaskHandlerDispatcherTests
{
    private sealed class StubHandler(string taskType, SyncResultDto result) : ITaskHandler
    {
        public string TaskType { get; } = taskType;

        public Task<SyncResultDto> HandleAsync(SyncTaskDto task, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static SyncTaskDto CreateTask(string taskType) => new()
    {
        TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        TaskType = taskType,
        Parameters = new Dictionary<string, string>(),
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task DispatchAsync_Routes_To_Matching_Handler()
    {
        var task = CreateTask("GetCustomers");
        var expected = new SyncResultDto
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = "completed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = null
        };

        var otherResult = new SyncResultDto
        {
            TaskId = task.TaskId,
            TaskType = "GetProducts",
            Status = "completed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = null
        };

        var handlers = new List<ITaskHandler>
        {
            new StubHandler("GetCustomers", expected),
            new StubHandler("GetProducts", otherResult)
        };

        var sut = new TaskHandlerDispatcher(handlers, NullLogger<TaskHandlerDispatcher>.Instance);

        var result = await sut.DispatchAsync(task, CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task DispatchAsync_Is_Case_Insensitive_When_Matching_TaskType()
    {
        var task = CreateTask("getcustomers");
        var expected = new SyncResultDto
        {
            TaskId = task.TaskId,
            TaskType = "GetCustomers",
            Status = "completed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = null
        };

        var handlers = new List<ITaskHandler> { new StubHandler("GetCustomers", expected) };
        var sut = new TaskHandlerDispatcher(handlers, NullLogger<TaskHandlerDispatcher>.Instance);

        var result = await sut.DispatchAsync(task, CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task DispatchAsync_Returns_Failed_Result_For_Unknown_TaskType()
    {
        var task = CreateTask("UnknownTaskType");
        var handlers = new List<ITaskHandler>();
        var sut = new TaskHandlerDispatcher(handlers, NullLogger<TaskHandlerDispatcher>.Instance);

        var result = await sut.DispatchAsync(task, CancellationToken.None);

        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
        Assert.Equal("failed", result.Status);
        Assert.Equal(0, result.RecordCount);
        Assert.Null(result.Data);
        Assert.Contains("No handler registered", result.ErrorMessage);
    }
}
