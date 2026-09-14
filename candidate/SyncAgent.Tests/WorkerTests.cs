using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Contracts;
using SyncAgent.Handlers;

namespace SyncAgent.Tests;

public class WorkerTests
{
    private sealed class StubHandler(string taskType, SyncResultDto result) : ITaskHandler
    {
        public string TaskType { get; } = taskType;

        public Task<SyncResultDto> HandleAsync(SyncTaskDto task, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static SyncTaskDto CreateTask() => new()
    {
        TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        TaskType = "GetCustomers",
        Parameters = new Dictionary<string, string>(),
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_Dispatches_Task_And_Posts_Result_When_Task_Available()
    {
        var task = CreateTask();
        var expectedResult = new SyncResultDto
        {
            TaskId = task.TaskId,
            TaskType = task.TaskType,
            Status = "completed",
            Data = null,
            RecordCount = 0,
            ExecutedAt = DateTime.UtcNow,
            ErrorMessage = null
        };

        var apiClientMock = new Mock<ISyncPlatformApiClient>();
        apiClientMock
            .SetupSequence(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(task)
            .ReturnsAsync((SyncTaskDto?)null);

        SyncResultDto? postedResult = null;
        apiClientMock
            .Setup(c => c.PostResultAsync(It.IsAny<SyncResultDto>(), It.IsAny<CancellationToken>()))
            .Callback<SyncResultDto, CancellationToken>((result, _) => postedResult = result)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(apiClientMock.Object);
        services.AddSingleton<ITaskHandler>(new StubHandler("GetCustomers", expectedResult));
        services.AddSingleton<TaskHandlerDispatcher>();
        services.AddSingleton<ILogger<TaskHandlerDispatcher>>(NullLogger<TaskHandlerDispatcher>.Instance);
        await using var provider = services.BuildServiceProvider();

        var options = Options.Create(new SyncAgentOptions
        {
            ApiBaseUrl = "http://localhost",
            ApiKey = "test-key",
            ConnectionString = "test-connection-string",
            PollIntervalSeconds = 60
        });

        var worker = new Worker(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<Worker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => postedResult is not null);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(postedResult);
        Assert.Equal(task.TaskId, postedResult!.TaskId);
        Assert.Equal("completed", postedResult.Status);
        apiClientMock.Verify(c => c.PostResultAsync(It.IsAny<SyncResultDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Does_Not_Post_Result_When_No_Task_Available()
    {
        var apiClientMock = new Mock<ISyncPlatformApiClient>();
        apiClientMock
            .Setup(c => c.GetNextTaskAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncTaskDto?)null);

        var services = new ServiceCollection();
        services.AddSingleton(apiClientMock.Object);
        services.AddSingleton<TaskHandlerDispatcher>();
        services.AddSingleton<ILogger<TaskHandlerDispatcher>>(NullLogger<TaskHandlerDispatcher>.Instance);
        await using var provider = services.BuildServiceProvider();

        var options = Options.Create(new SyncAgentOptions
        {
            ApiBaseUrl = "http://localhost",
            ApiKey = "test-key",
            ConnectionString = "test-connection-string",
            PollIntervalSeconds = 60
        });

        var worker = new Worker(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<Worker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => apiClientMock.Invocations.Count(i => i.Method.Name == nameof(ISyncPlatformApiClient.GetNextTaskAsync)) >= 1);
        await worker.StopAsync(CancellationToken.None);

        apiClientMock.Verify(c => c.PostResultAsync(It.IsAny<SyncResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            await Task.Delay(20);
        }
    }
}
