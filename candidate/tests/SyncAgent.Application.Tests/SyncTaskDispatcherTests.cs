using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using Xunit;

namespace SyncAgent.Application.Tests;

public class SyncTaskDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_RoutesToTheHandlerMatchingTaskType()
    {
        var customersHandler = new Mock<ISyncTaskHandler>();
        customersHandler.SetupGet(h => h.TaskType).Returns("GetCustomers");
        customersHandler
            .Setup(h => h.ExecuteAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SyncTaskExecutionResult.Completed(Array.Empty<object>()));

        var productsHandler = new Mock<ISyncTaskHandler>();
        productsHandler.SetupGet(h => h.TaskType).Returns("GetProducts");

        var dispatcher = new SyncTaskDispatcher(
            [customersHandler.Object, productsHandler.Object],
            NullLogger<SyncTaskDispatcher>.Instance);

        await dispatcher.DispatchAsync("GetCustomers", [], CancellationToken.None);

        customersHandler.Verify(
            h => h.ExecuteAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        productsHandler.Verify(
            h => h.ExecuteAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WithUnknownTaskType_ReturnsFailureInsteadOfThrowing()
    {
        var dispatcher = new SyncTaskDispatcher([], NullLogger<SyncTaskDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync("GetEmployees", [], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("GetEmployees", result.ErrorMessage);
    }
}
