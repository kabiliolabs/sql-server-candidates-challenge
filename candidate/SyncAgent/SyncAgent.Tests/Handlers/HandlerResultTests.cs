using Moq;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Handlers;
using Xunit;

namespace SyncAgent.Tests.Handlers;

/// <summary>
/// Keeps a few focused handler tests around the contract that matters:
/// the result shape and the propagation of modifiedSince to the repository.
/// </summary>
public class HandlerResultTests
{
    private static SyncTask MakeTask(string type, DateTime? modifiedSince = null) => new()
    {
        TaskId = "test-id-123",
        TaskType = type,
        Parameters = modifiedSince.HasValue
            ? new Dictionary<string, object?> { ["modifiedSince"] = modifiedSince.Value.ToString("O") }
            : new Dictionary<string, object?>()
    };

    private static CustomerDto FakeCustomer(int id) => new(
        id, "First", "Last", 0, "email@test.com",
        "Street 1", null, "City", "State", "Country", "00000", DateTime.UtcNow);

    [Fact]
    public async Task GetCustomersHandler_ReturnsCompleted_WithCorrectTaskId()
    {
        var fakeData = new List<CustomerDto> { FakeCustomer(1), FakeCustomer(2) };
        var repo = new Mock<IAdventureWorksRepository>();
        repo.Setup(r => r.GetCustomersAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeData);

        var result = await new GetCustomersHandler(repo.Object).HandleAsync(MakeTask("GetCustomers"));

        Assert.Equal("test-id-123", result.TaskId);
        Assert.Equal("GetCustomers", result.TaskType);
        Assert.Equal("completed", result.Status);
        Assert.Equal(2, result.RecordCount);
    }

    [Fact]
    public async Task GetCustomersHandler_PassesModifiedSince_ToRepository()
    {
        var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var repo = new Mock<IAdventureWorksRepository>();
        repo.Setup(r => r.GetCustomersAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerDto>());

        await new GetCustomersHandler(repo.Object).HandleAsync(MakeTask("GetCustomers", cutoff));

        repo.Verify(r => r.GetCustomersAsync(
            It.Is<DateTime?>(d => d.HasValue && d.Value.Date == cutoff.Date),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrdersHandler_PassesNullModifiedSince_WhenNoParameter()
    {
        var repo = new Mock<IAdventureWorksRepository>();
        repo.Setup(r => r.GetOrdersAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderDto>());

        await new GetOrdersHandler(repo.Object).HandleAsync(MakeTask("GetOrders"));

        repo.Verify(r => r.GetOrdersAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
