using Moq;
using SyncAgent.Data;
using SyncAgent.Data.Dto;
using SyncAgent.Handlers;
using SyncAgent.Models;

namespace SyncAgent.Tests.Handlers;

public class GetOrdersHandlerTests
{
    private readonly Mock<IAdventureWorksRepository> _repoMock = new();
    private readonly GetOrdersHandler _handler;

    public GetOrdersHandlerTests()
    {
        _handler = new GetOrdersHandler(_repoMock.Object);
    }

    [Fact]
    public void TaskType_Returns_GetOrders()
    {
        Assert.Equal("GetOrders", _handler.TaskType);
    }

    [Fact]
    public async Task ExecuteAsync_Groups_FlatRows_Into_Orders_With_Nested_Details()
    {
        // Two rows for the same order, one row for a different order
        var flatRows = new List<OrderFlatRow>
        {
            new() { SalesOrderId = 1, OrderDate = DateTime.UtcNow, Status = 5,
                    CustomerName = "Alice", AccountNumber = "AW001", TotalDue = 100m,
                    ProductName = "Bike", ProductNumber = "BK-001", UnitPrice = 50m, Quantity = 1, LineTotal = 50m },
            new() { SalesOrderId = 1, OrderDate = DateTime.UtcNow, Status = 5,
                    CustomerName = "Alice", AccountNumber = "AW001", TotalDue = 100m,
                    ProductName = "Helmet", ProductNumber = "HL-001", UnitPrice = 50m, Quantity = 1, LineTotal = 50m },
            new() { SalesOrderId = 2, OrderDate = DateTime.UtcNow, Status = 5,
                    CustomerName = "Bob", AccountNumber = "AW002", TotalDue = 200m,
                    ProductName = "Frame", ProductNumber = "FR-001", UnitPrice = 200m, Quantity = 1, LineTotal = 200m }
        };

        _repoMock.Setup(r => r.GetOrderFlatRowsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(flatRows);

        var task = new SyncTask { TaskId = "task-3", TaskType = "GetOrders" };
        var result = await _handler.ExecuteAsync(task);

        Assert.Equal("completed", result.Status);
        // 3 flat rows → 2 grouped orders
        Assert.Equal(2, result.RecordCount);
    }

    [Fact]
    public async Task ExecuteAsync_SingleOrder_MultipleDetails_Has_RecordCount_One()
    {
        var flatRows = new List<OrderFlatRow>
        {
            new() { SalesOrderId = 10, CustomerName = "X", AccountNumber = "AW010", TotalDue = 10m,
                    ProductName = "P1", ProductNumber = "PN1", UnitPrice = 5m, Quantity = 1, LineTotal = 5m },
            new() { SalesOrderId = 10, CustomerName = "X", AccountNumber = "AW010", TotalDue = 10m,
                    ProductName = "P2", ProductNumber = "PN2", UnitPrice = 5m, Quantity = 1, LineTotal = 5m }
        };

        _repoMock.Setup(r => r.GetOrderFlatRowsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(flatRows);

        var task = new SyncTask { TaskId = "task-3", TaskType = "GetOrders" };
        var result = await _handler.ExecuteAsync(task);

        Assert.Equal(1, result.RecordCount);
    }
}
