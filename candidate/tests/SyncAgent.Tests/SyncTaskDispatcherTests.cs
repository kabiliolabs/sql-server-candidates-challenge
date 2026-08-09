using SyncAgent.Handlers;
using SyncAgent.Models;
using SyncAgent.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Tests;

public class SyncTaskDispatcherTests
{
    [Fact]
    public async Task ExecuteAsync_UsesCorrectHandler()
    {
        // Arrange
        var expectedExecution =
            new SyncTaskExecution(
                new[] { "test-result" },
                1);

        var handler = new FakeHandler(
            "GetProducts",
            expectedExecution);

        var dispatcher = new SyncTaskDispatcher(
            new[] { handler });

        var task = CreateTask("GetProducts");

        // Act
        var result = await dispatcher.ExecuteAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.Same(expectedExecution, result);
        Assert.True(handler.WasExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_IsCaseInsensitive()
    {
        // Arrange
        var handler = new FakeHandler(
            "GetProducts",
            new SyncTaskExecution(
                Array.Empty<object>(),
                0));

        var dispatcher = new SyncTaskDispatcher(
            new[] { handler });

        var task = CreateTask("getproducts");

        // Act
        await dispatcher.ExecuteAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.True(handler.WasExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForUnsupportedTaskType()
    {
        // Arrange
        var dispatcher = new SyncTaskDispatcher(
            Array.Empty<ISyncTaskHandler>());

        var task = CreateTask("UnknownTask");

        // Act + Assert
        var exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () => dispatcher.ExecuteAsync(
                    task,
                    CancellationToken.None));

        Assert.Contains(
            "Unsupported task type",
            exception.Message);
    }

    private static SyncTask CreateTask(
        string taskType)
    {
        return new SyncTask
        {
            TaskId = "test-task-id",
            TaskType = taskType,
            CreatedAt = DateTimeOffset.UtcNow,
            Parameters = new SyncTaskParameters
            {
                ModifiedSince =
                    DateTimeOffset.UtcNow.AddDays(-1)
            }
        };
    }

    private sealed class FakeHandler
        : ISyncTaskHandler
    {
        private readonly SyncTaskExecution _execution;

        public FakeHandler(
            string taskType,
            SyncTaskExecution execution)
        {
            TaskType = taskType;
            _execution = execution;
        }

        public string TaskType { get; }

        public bool WasExecuted { get; private set; }

        public Task<SyncTaskExecution> ExecuteAsync(
            SyncTask task,
            CancellationToken cancellationToken)
        {
            WasExecuted = true;

            return Task.FromResult(_execution);
        }
    }
}