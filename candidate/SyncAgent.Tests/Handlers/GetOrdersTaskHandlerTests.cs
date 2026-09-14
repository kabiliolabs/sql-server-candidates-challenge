using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncAgent.Contracts;
using SyncAgent.Data;
using SyncAgent.Handlers;

namespace SyncAgent.Tests.Handlers;

public class GetOrdersTaskHandlerTests
{
    private static SyncTaskDto CreateTask(string? modifiedSince = "2025-01-01T00:00:00Z") => new()
    {
        TaskId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        TaskType = "GetOrders",
        Parameters = modifiedSince is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["modifiedSince"] = modifiedSince },
        CreatedAt = DateTime.UtcNow
    };

    private static OrderDto CreateMultiLineOrder() => new()
    {
        SalesOrderId = 1,
        OrderDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = 5,
        CustomerName = "John Smith",
        AccountNumber = "AW00000001",
        TotalDue = 500m,
        OrderDetails =
        [
            new OrderDetailDto { ProductName = "Road-150", ProductNumber = "BK-R93R-62", UnitPrice = 300m, Quantity = 1, LineTotal = 300m },
            new OrderDetailDto { ProductName = "Sport-100", ProductNumber = "HL-U509-R", UnitPrice = 100m, Quantity = 2, LineTotal = 200m }
        ]
    };

    [Fact]
    public async Task HandleAsync_Returns_Completed_Result_With_Matching_TaskId_And_Multi_Line_Order()
    {
        var repoMock = new Mock<IAdventureWorksRepository>();
        repoMock
            .Setup(r => r.GetOrdersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderDto> { CreateMultiLineOrder() });

        var sut = new GetOrdersTaskHandler(repoMock.Object, NullLogger<GetOrdersTaskHandler>.Instance);
        var task = CreateTask();

        var result = await sut.HandleAsync(task, CancellationToken.None);

        Assert.Equal(task.TaskId, result.TaskId);
        Assert.Equal(task.TaskType, result.TaskType);
        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.RecordCount);
        Assert.NotNull(result.Data);
        Assert.Null(result.ErrorMessage);

        var dataArray = result.Data!.Value;
        var orderDetails = dataArray[0].GetProperty("orderDetails");
        Assert.Equal(2, orderDetails.GetArrayLength());
    }

    [Fact]
    public async Task HandleAsync_Returns_Failed_Result_With_Matching_TaskId_When_Repository_Throws()
    {
        var repoMock = new Mock<IAdventureWorksRepository>();
        repoMock
            .Setup(r => r.GetOrdersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var sut = new GetOrdersTaskHandler(repoMock.Object, NullLogger<GetOrdersTaskHandler>.Instance);
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
            .Setup(r => r.GetOrdersAsync(DateTime.MinValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderDto>());

        var sut = new GetOrdersTaskHandler(repoMock.Object, NullLogger<GetOrdersTaskHandler>.Instance);
        var task = CreateTask(modifiedSince: null);

        var result = await sut.HandleAsync(task, CancellationToken.None);

        Assert.Equal("completed", result.Status);
        repoMock.Verify(r => r.GetOrdersAsync(DateTime.MinValue, It.IsAny<CancellationToken>()), Times.Once);
    }
}
