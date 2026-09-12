using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Infrastructure.Sql;
using Xunit;

namespace SyncAgent.Infrastructure.IntegrationTests;

// These tests hit the real, locally-restored AdventureWorks2025 database via
// Microsoft.Data.SqlClient. They require a live SQL Server instance reachable at
// the connection string below (Windows Integrated Auth against localhost\SQLEXPRESS).
// Kept separate from SyncAgent.Infrastructure.Tests (which use mocked/fake dependencies
// and never touch a real database) per the challenge's own guidance to clearly
// separate unit tests from integration tests.
public class RepositoryTests
{
    private static readonly DatabaseOptions Options = new()
    {
        ConnectionString = "Server=localhost\\SQLEXPRESS;Database=AdventureWorks2025;Trusted_Connection=True;TrustServerCertificate=True;"
    };

    // 2000-01-01 is far enough in the past that it matches all seed data in AdventureWorks
    // (whose ModifiedDate values are all <= 2014), proving the query/mapping logic works
    // end-to-end against the real schema.
    private static readonly DateTime FarPast = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CustomerRepository_GetCustomersAsync_ReturnsRealCustomers()
    {
        var repository = new CustomerRepository(Options.ToOptionsWrapper(), NullLogger<CustomerRepository>.Instance);

        var customers = await repository.GetCustomersAsync(FarPast, CancellationToken.None);

        Assert.NotEmpty(customers);
        var jonYang = customers.Single(c => c.CustomerId == 11000);
        Assert.Equal("AW00011000", jonYang.AccountNumber);
        Assert.Equal("Jon", jonYang.FirstName);
        Assert.Equal("Yang", jonYang.LastName);
        Assert.Equal("jon24@adventure-works.com", jonYang.EmailAddress);
    }

    [Fact]
    public async Task ProductRepository_GetProductsAsync_ReturnsRealProducts()
    {
        var repository = new ProductRepository(Options.ToOptionsWrapper(), NullLogger<ProductRepository>.Instance);

        var products = await repository.GetProductsAsync(FarPast, CancellationToken.None);

        Assert.NotEmpty(products);
        var frame = products.Single(p => p.ProductId == 680);
        Assert.Equal("HL Road Frame - Black, 58", frame.Name);
        Assert.Equal("Components", frame.Category);
        Assert.Equal("Road Frames", frame.Subcategory);

        // Products with no subcategory (e.g. raw materials) must still come back
        // with null Category/Subcategory rather than being excluded (LEFT JOIN, not INNER).
        Assert.Contains(products, p => p.Subcategory == null);
    }

    [Fact]
    public async Task OrderRepository_GetOrdersAsync_ReturnsRealOrdersWithLineItems()
    {
        var repository = new OrderRepository(Options.ToOptionsWrapper(), NullLogger<OrderRepository>.Instance);

        var orders = await repository.GetOrdersAsync(FarPast, CancellationToken.None);

        Assert.NotEmpty(orders);
        var order = orders.Single(o => o.SalesOrderId == 43659);
        Assert.Equal("James Hendergart", order.CustomerName);
        Assert.Equal("AW00029825", order.AccountNumber);
        Assert.NotEmpty(order.OrderDetails);
        Assert.Contains(order.OrderDetails, d => d.ProductNumber == "BK-M82B-42");
    }

    [Fact]
    public async Task InventoryRepository_GetInventoryAsync_ReturnsRealInventory()
    {
        var repository = new InventoryRepository(Options.ToOptionsWrapper(), NullLogger<InventoryRepository>.Instance);

        var items = await repository.GetInventoryAsync(FarPast, CancellationToken.None);

        Assert.NotEmpty(items);
        var item = items.First(i => i.ProductId == 1);
        Assert.Equal("Adjustable Race", item.ProductName);
        Assert.Equal("Tool Crib", item.LocationName);
    }

    // AdventureWorks seed data has no ModifiedDate later than 2014, so a cut-off in the future
    // must filter every row out. These pin the ModifiedSince filter itself: without them, a query
    // that silently ignored its parameter would still pass every other test in this file.
    [Fact]
    public async Task Repositories_WithFutureModifiedSince_ReturnNoRecords()
    {
        var future = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Empty(await new CustomerRepository(Options.ToOptionsWrapper(), NullLogger<CustomerRepository>.Instance)
            .GetCustomersAsync(future, CancellationToken.None));
        Assert.Empty(await new ProductRepository(Options.ToOptionsWrapper(), NullLogger<ProductRepository>.Instance)
            .GetProductsAsync(future, CancellationToken.None));
        Assert.Empty(await new OrderRepository(Options.ToOptionsWrapper(), NullLogger<OrderRepository>.Instance)
            .GetOrdersAsync(future, CancellationToken.None));
        Assert.Empty(await new InventoryRepository(Options.ToOptionsWrapper(), NullLogger<InventoryRepository>.Instance)
            .GetInventoryAsync(future, CancellationToken.None));
    }
}

internal static class OptionsTestExtensions
{
    public static IOptions<DatabaseOptions> ToOptionsWrapper(this DatabaseOptions options) => Microsoft.Extensions.Options.Options.Create(options);
}
