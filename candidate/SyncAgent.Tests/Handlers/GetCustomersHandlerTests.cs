using Moq;
using SyncAgent.Data;
using SyncAgent.Data.Dto;
using SyncAgent.Handlers;
using SyncAgent.Models;

namespace SyncAgent.Tests.Handlers;

public class GetCustomersHandlerTests
{
    private readonly Mock<IAdventureWorksRepository> _repoMock = new();
    private readonly GetCustomersHandler _handler;

    public GetCustomersHandlerTests()
    {
        _handler = new GetCustomersHandler(_repoMock.Object);
    }

    [Fact]
    public void TaskType_Returns_GetCustomers()
    {
        Assert.Equal("GetCustomers", _handler.TaskType);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Completed_With_MappedData()
    {
        var customers = new List<CustomerDto>
        {
            new() { CustomerId = 1, AccountNumber = "AW001", FirstName = "John", LastName = "Doe",
                    EmailAddress = "john@example.com", Phone = "555-1234",
                    AddressLine1 = "123 Main St", City = "Springfield",
                    StateProvince = "IL", PostalCode = "62701", CountryRegion = "US" }
        };
        _repoMock.Setup(r => r.GetCustomersAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(customers);

        var task = new SyncTask
        {
            TaskId = "task-1", TaskType = "GetCustomers",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" }
        };

        var result = await _handler.ExecuteAsync(task);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.RecordCount);
        Assert.NotNull(result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Passes_ModifiedSince_To_Repository()
    {
        _repoMock.Setup(r => r.GetCustomersAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Enumerable.Empty<CustomerDto>());

        var task = new SyncTask
        {
            TaskId = "task-1", TaskType = "GetCustomers",
            Parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-06-15T00:00:00Z" }
        };

        await _handler.ExecuteAsync(task);

        _repoMock.Verify(r => r.GetCustomersAsync(
            It.Is<DateTime?>(d => d.HasValue && d.Value.Year == 2025 && d.Value.Month == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoModifiedSince_Passes_Null_To_Repository()
    {
        _repoMock.Setup(r => r.GetCustomersAsync(null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Enumerable.Empty<CustomerDto>());

        var task = new SyncTask { TaskId = "task-1", TaskType = "GetCustomers" };

        await _handler.ExecuteAsync(task);

        _repoMock.Verify(r => r.GetCustomersAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
