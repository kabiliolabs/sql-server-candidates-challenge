using Moq;
using SyncAgent.Data;
using SyncAgent.Data.Dto;
using SyncAgent.Handlers;
using SyncAgent.Models;

namespace SyncAgent.Tests.Handlers;

public class GetProductsHandlerTests
{
    private readonly Mock<IAdventureWorksRepository> _repoMock = new();
    private readonly GetProductsHandler _handler;

    public GetProductsHandlerTests()
    {
        _handler = new GetProductsHandler(_repoMock.Object);
    }

    [Fact]
    public void TaskType_Returns_GetProducts()
    {
        Assert.Equal("GetProducts", _handler.TaskType);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Completed_With_MappedData()
    {
        var products = new List<ProductDto>
        {
            new() { ProductId = 680, Name = "HL Road Frame", ProductNumber = "FR-R92B-58",
                    Color = "Black", StandardCost = 1059.31m, ListPrice = 1431.50m,
                    Category = "Components", Subcategory = "Road Frames",
                    ModifiedDate = new DateTime(2025, 2, 7) }
        };
        _repoMock.Setup(r => r.GetProductsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(products);

        var task = new SyncTask { TaskId = "task-2", TaskType = "GetProducts",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" } };

        var result = await _handler.ExecuteAsync(task);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.RecordCount);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyResult_Returns_Zero_RecordCount()
    {
        _repoMock.Setup(r => r.GetProductsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Enumerable.Empty<ProductDto>());

        var task = new SyncTask { TaskId = "task-2", TaskType = "GetProducts" };
        var result = await _handler.ExecuteAsync(task);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.RecordCount);
    }
}
