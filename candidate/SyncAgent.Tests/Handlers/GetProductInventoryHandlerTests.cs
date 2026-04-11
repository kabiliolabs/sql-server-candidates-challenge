using Moq;
using SyncAgent.Data;
using SyncAgent.Data.Dto;
using SyncAgent.Handlers;
using SyncAgent.Models;

namespace SyncAgent.Tests.Handlers;

public class GetProductInventoryHandlerTests
{
    private readonly Mock<IAdventureWorksRepository> _repoMock = new();
    private readonly GetProductInventoryHandler _handler;

    public GetProductInventoryHandlerTests()
    {
        _handler = new GetProductInventoryHandler(_repoMock.Object);
    }

    [Fact]
    public void TaskType_Returns_GetProductInventory()
    {
        Assert.Equal("GetProductInventory", _handler.TaskType);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Completed_With_MappedData()
    {
        var inventory = new List<ProductInventoryDto>
        {
            new() { ProductId = 1, ProductName = "Adjustable Race", ProductNumber = "AR-5381",
                    LocationName = "Tool Crib", Shelf = "A", Bin = 1, Quantity = 408,
                    ModifiedDate = new DateTime(2025, 8, 7) }
        };
        _repoMock.Setup(r => r.GetProductInventoryAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(inventory);

        var task = new SyncTask { TaskId = "task-4", TaskType = "GetProductInventory",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" } };

        var result = await _handler.ExecuteAsync(task);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.RecordCount);
        Assert.Null(result.ErrorMessage);
    }
}
