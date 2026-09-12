using System.Text.Json;
using Moq;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using SyncAgent.Domain.Models;
using Xunit;

namespace SyncAgent.Application.Tests;

public class GetCustomersHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidParameters_ReturnsMappedCustomersWithExactContractFieldNames()
    {
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetCustomersAsync(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Customer
                {
                    CustomerId = 11000,
                    AccountNumber = "AW00011000",
                    FirstName = "Jon",
                    LastName = "Yang",
                    EmailAddress = "jon24@adventure-works.com"
                }
            ]);
        var handler = new GetCustomersHandler(repository.Object);
        var parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" };

        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordCount);
        Assert.Null(result.ErrorMessage);

        // Asserting on the serialized JSON pins the exact camelCase field names the contract requires,
        // which the C# property names alone would not.
        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("\"customerId\":11000", json);
        Assert.Contains("\"accountNumber\":\"AW00011000\"", json);
        Assert.Contains("\"firstName\":\"Jon\"", json);
        Assert.Contains("\"emailAddress\":\"jon24@adventure-works.com\"", json);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingModifiedSince_FailsValidationWithoutQueryingRepository()
    {
        var repository = new Mock<ICustomerRepository>();
        var handler = new GetCustomersHandler(repository.Object);
        var parameters = new Dictionary<string, string>(); // no "modifiedSince" key at all

        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordCount);
        Assert.Null(result.Data);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        repository.Verify(
            r => r.GetCustomersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroMatchingRecords_ReturnsSuccessWithEmptyData()
    {
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetCustomersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new GetCustomersHandler(repository.Object);
        var parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" };

        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_PropagatesOperationCanceledExceptionInsteadOfReportingFailure()
    {
        // Cancellation must reach the poller instead of being reported to the platform as a
        // failed task, so the catch-all must not absorb it.
        var repository = new Mock<ICustomerRepository>();
        repository
            .Setup(r => r.GetCustomersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var handler = new GetCustomersHandler(repository.Object);
        var parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(parameters, CancellationToken.None));
    }
}

public class GetOrdersHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_MapsNestedOrderLinesWithExactContractFieldNames()
    {
        var repository = new Mock<IOrderRepository>();
        repository
            .Setup(r => r.GetOrdersAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Order
                {
                    SalesOrderId = 43659,
                    OrderDate = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc),
                    Status = 5,
                    CustomerName = "James Hendergart",
                    AccountNumber = "AW00029825",
                    TotalDue = 23153.2339m,
                    OrderDetails =
                    [
                        new OrderLine
                        {
                            ProductName = "Mountain-100 Black, 42",
                            ProductNumber = "BK-M82B-42",
                            UnitPrice = 2024.994m,
                            Quantity = 1,
                            LineTotal = 2024.994m
                        }
                    ]
                }
            ]);
        var handler = new GetOrdersHandler(repository.Object);
        var parameters = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" };

        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordCount);

        var json = JsonSerializer.Serialize(result.Data);
        Assert.Contains("\"salesOrderId\":43659", json);
        Assert.Contains("\"customerName\":\"James Hendergart\"", json);
        Assert.Contains("\"accountNumber\":\"AW00029825\"", json); // must be the Customer's account number, not the order header's
        Assert.Contains("\"orderDetails\":[{", json);
        Assert.Contains("\"productNumber\":\"BK-M82B-42\"", json);
    }
}
