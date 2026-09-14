using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncAgent.Contracts;
using SyncAgent.Data;
using SyncAgent.Handlers;

namespace SyncAgent.Tests.Handlers;

public class GetProductsTaskHandlerTests
{
    private static SyncTaskDto CreateTask(string? modifiedSince = "2025-01-01T00:00:00Z") => new()
    {
        TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        TaskType = "GetProducts",
        Parameters = modifiedSince is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["modifiedSince"] = modifiedSince },
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task HandleAsync_Returns_Completed_Result_With_Matching_TaskId()
    {
        var repoMock = new Mock<IAdventureWorksRepository>();
        repoMock
            .Setup(r => r.GetProductsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto> { new() { ProductId = 1, Name = "Mountain-100" } });

        var sut = new GetProductsTaskHandler(repoMock.Object, NullLogger<GetProductsTaskHandler>.Instance);
        var task = CreateTask();

        var result = await sut.HandleAsync(task, CancellationToken.None);

        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.RecordCount);
        Assert.NotNull(result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_Returns_Failed_Result_With_Matching_TaskId_When_Repository_Throws()
    {
        var repoMock = new Mock<IAdventureWorksRepository>();
        repoMock
            .Setup(r => r.GetProductsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var sut = new GetProductsTaskHandler(repoMock.Object, NullLogger<GetProductsTaskHandler>.Instance);
        var task = CreateTask();

        var result = await sut.HandleAsync(task, CancellationToken.None);

        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
        Assert.Equal("failed", result.Status);
        Assert.Equal(0, result.RecordCount);
        Assert.Null(result.Data);
        Assert.Equal("DB connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_Falls_Back_Gracefully_When_ModifiedSince_Missing()
    {
        var repoMock = new Mock<IAdventureWorksRepository>();
        repoMock
            .Setup(r => r.GetProductsAsync(DateTime.MinValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());

        var sut = new GetProductsTaskHandler(repoMock.Object, NullLogger<GetProductsTaskHandler>.Instance);
        var task = CreateTask(modifiedSince: null);

        var result = await sut.HandleAsync(task, CancellationToken.None);

        Assert.Equal("completed", result.Status);
        repoMock.Verify(r => r.GetProductsAsync(DateTime.MinValue, It.IsAny<CancellationToken>()), Times.Once);
    }
}
