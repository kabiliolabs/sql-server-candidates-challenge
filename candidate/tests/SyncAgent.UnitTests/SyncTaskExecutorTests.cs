using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Contracts;
using SyncAgent.Services;
using SyncAgent.TaskHandlers;
using SyncAgent.Validation;
using Xunit;

namespace SyncAgent.UnitTests;

public sealed class SyncTaskExecutorTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 12, 10, 30, 5, TimeSpan.Zero));
    private readonly ISyncTaskValidator _validator = new SyncTaskValidator();

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCompletedResult_WhenHandlerSucceeds()
    {
        var task = CreateTask("GetProducts");
        var handler = new FakeHandler("GetProducts", [new { ProductId = 1 }]);
        var executor = new SyncTaskExecutor([handler], _validator, _timeProvider, NullLogger<SyncTaskExecutor>.Instance);

        var result = await executor.ExecuteAsync(task, CancellationToken.None);

        result.Status.Should().Be("completed");
        result.RecordCount.Should().Be(1);
        result.ErrorMessage.Should().BeNull();
        result.ExecutedAt.Should().Be(_timeProvider.FixedUtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailedResult_WhenHandlerThrows()
    {
        var task = CreateTask("GetProducts");
        var handler = new ThrowingHandler("GetProducts", new InvalidOperationException("Boom"));
        var executor = new SyncTaskExecutor([handler], _validator, _timeProvider, NullLogger<SyncTaskExecutor>.Instance);

        var result = await executor.ExecuteAsync(task, CancellationToken.None);

        result.Status.Should().Be("failed");
        result.RecordCount.Should().Be(0);
        result.Data.Should().BeNull();
        result.ErrorMessage.Should().Be("Boom");
    }

    private static SyncTaskContract CreateTask(string taskType) => new()
    {
        TaskId = "01JQFG9A7BMPQ3ND6XWZR1E8HY",
        TaskType = taskType,
        Parameters = new SyncTaskParametersContract
        {
            ModifiedSince = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        CreatedAt = new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero)
    };

    private sealed class FakeHandler(string taskType, IReadOnlyCollection<object> result) : ISyncTaskHandler
    {
        public string TaskType => taskType;
        public Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ThrowingHandler(string taskType, Exception exception) : ISyncTaskHandler
    {
        public string TaskType => taskType;
        public Task<IReadOnlyCollection<object>> HandleAsync(SyncTaskContract task, CancellationToken cancellationToken) => Task.FromException<IReadOnlyCollection<object>>(exception);
    }

    private sealed class FakeTimeProvider(DateTimeOffset fixedUtcNow) : TimeProvider
    {
        public DateTimeOffset FixedUtcNow { get; } = fixedUtcNow;
        public override DateTimeOffset GetUtcNow() => FixedUtcNow;
    }
}
