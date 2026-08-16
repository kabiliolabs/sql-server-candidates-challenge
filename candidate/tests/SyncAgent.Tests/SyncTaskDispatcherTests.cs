using SyncAgent.Repository;
using SyncAgent.Models;
using SyncAgent.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Tests;

public class SyncTaskDispatcherTests
{
    [Fact]
    public async Task ExecuteAsync_DispatchesToRepositoryMethod()
    {
        // Arrange
        var repository = new FakeRepository();

        var dispatcher = new SyncTaskDispatcher(repository);

        var task = CreateTask("GetProducts");

        // Act
        var result = await dispatcher.ExecuteAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.Equal("GetProducts", repository.LastCalled);
        Assert.Equal(repository.Products.Count, result.RecordCount);
        Assert.Same(repository.Products, result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_IsCaseSensitive()
    {
        // Arrange
        var dispatcher = new SyncTaskDispatcher(new FakeRepository());

        var task = CreateTask("getproducts");

        // Act + Assert
        var exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () => dispatcher.ExecuteAsync(
                    task,
                    CancellationToken.None));

        Assert.Contains(
            "Unsupported task type",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForUnsupportedTaskType()
    {
        // Arrange
        var dispatcher = new SyncTaskDispatcher(new FakeRepository());

        var task = CreateTask("UnknownTask");

        // Act + Assert
        var exception =
            await Assert.ThrowsAsync<NotSupportedException>(
                () => dispatcher.ExecuteAsync(
                    task,
                    CancellationToken.None));

        Assert.Contains(
            "Unsupported task type",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenModifiedSinceIsMissing()
    {
        // Arrange
        var dispatcher = new SyncTaskDispatcher(new FakeRepository());

        var task = new SyncTask
        {
            TaskId = "test-task-id",
            TaskType = "GetProducts",
            CreatedAt = DateTimeOffset.UtcNow,
            Parameters = new SyncTaskParameters()
        };

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => dispatcher.ExecuteAsync(
                task,
                CancellationToken.None));
    }

    [Theory]
    [InlineData("GetCustomers")]
    [InlineData("GetOrders")]
    [InlineData("GetProductInventory")]
    public async Task ExecuteAsync_DispatchesRemainingTaskTypes(
        string taskType)
    {
        // Arrange
        var repository = new FakeRepository();

        var dispatcher = new SyncTaskDispatcher(repository);

        var task = CreateTask(taskType);

        // Act
        var result = await dispatcher.ExecuteAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.Equal(taskType, repository.LastCalled);
        Assert.Equal(1, result.RecordCount);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsModifiedSince()
    {
        // Arrange
        var repository = new FakeRepository();

        var dispatcher = new SyncTaskDispatcher(repository);

        var modifiedSince = new DateTime(
            2025, 1, 1,
            0, 0, 0,
            DateTimeKind.Utc);

        var task = CreateTask("GetProducts") with
        {
            Parameters = new SyncTaskParameters
            {
                ModifiedSince =
                    new DateTimeOffset(modifiedSince)
            }
        };

        // Act
        await dispatcher.ExecuteAsync(
            task,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            modifiedSince,
            repository.LastModifiedSince);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        // Arrange
        var repository = new FakeRepository();

        var dispatcher = new SyncTaskDispatcher(repository);

        var task = CreateTask("GetProducts");

        using var cts = new CancellationTokenSource();

        // Act
        await dispatcher.ExecuteAsync(
            task,
            cts.Token);

        // Assert
        Assert.Equal(
            cts.Token,
            repository.LastCancellationToken);
    }

    private static SyncTask CreateTask(
        string taskType)
    {
        return new SyncTask
        {
            TaskId = "test-task-id",
            TaskType = taskType,
            CreatedAt = DateTimeOffset.UtcNow,
            Parameters = new SyncTaskParameters
            {
                ModifiedSince =
                    DateTimeOffset.UtcNow.AddDays(-1)
            }
        };
    }

    private sealed class FakeRepository
        : IAdventureWorksRepository
    {
        public IReadOnlyList<ProductDto> Products { get; } =
            new[] { new ProductDto() };

        public IReadOnlyList<CustomerDto> Customers { get; } =
            new[] { new CustomerDto() };

        public IReadOnlyList<OrderDto> Orders { get; } =
            new[] { new OrderDto() };

        public IReadOnlyList<ProductInventoryDto> Inventory { get; } =
            new[] { new ProductInventoryDto() };

        public string? LastCalled { get; private set; }

        public DateTime LastModifiedSince { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(
            DateTime modifiedSince,
            CancellationToken cancellationToken)
        {
            LastCalled = "GetCustomers";
            LastModifiedSince = modifiedSince;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Customers);
        }

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
            DateTime modifiedSince,
            CancellationToken cancellationToken)
        {
            LastCalled = "GetProducts";
            LastModifiedSince = modifiedSince;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Products);
        }

        public Task<IReadOnlyList<OrderDto>> GetOrdersAsync(
            DateTime modifiedSince,
            CancellationToken cancellationToken)
        {
            LastCalled = "GetOrders";
            LastModifiedSince = modifiedSince;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Orders);
        }

        public Task<IReadOnlyList<ProductInventoryDto>> GetProductInventoryAsync(
            DateTime modifiedSince,
            CancellationToken cancellationToken)
        {
            LastCalled = "GetProductInventory";
            LastModifiedSince = modifiedSince;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(Inventory);
        }
    }
}
