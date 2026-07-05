using FluentAssertions;
using Microsoft.Extensions.Options;
using TaskExecutionService.Configuration;
using TaskExecutionService.Data;
using TaskExecutionService.Models;
using TaskExecutionService.Sync;
using TaskExecutionService.Sync.Executors;
using Xunit;

namespace TaskExecutionService.Tests;

/// <summary>
/// End-to-end checks against a real AdventureWorks2025 database: they confirm the
/// SQL and the Dapper column mapping actually reproduce the documented sample
/// rows. Opt-in only — set the environment variable SYNC_DB_TESTS=1 to run them;
/// otherwise they are skipped so the unit suite stays green without a database.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DbIntegrationTests
{
    // Far enough in the past to include every sample row regardless of ModifiedDate.
    private static readonly SyncParameters AllRecords =
        SyncParameters.Parse(new Dictionary<string, string> { ["modifiedSince"] = "2000-01-01T00:00:00Z" });

    private static IDbConnectionFactory CreateFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("SYNC_DB_CONNECTION")
            ?? "Server=localhost;Database=AdventureWorks2025;Integrated Security=True;Encrypt=False;TrustServerCertificate=True";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new SqlConnectionFactory(options);
    }

    private static void SkipUnlessEnabled() =>
        Skip.IfNot(Environment.GetEnvironmentVariable("SYNC_DB_TESTS") == "1", "Set SYNC_DB_TESTS=1 to run DB integration tests.");

    [SkippableFact]
    public async Task GetCustomers_ReturnsExpectedSampleRow()
    {
        SkipUnlessEnabled();
        var rows = (await new GetCustomersExecutor(CreateFactory()).ExecuteAsync(AllRecords, CancellationToken.None))
            .Cast<CustomerDto>().ToList();

        var jon = rows.Single(c => c.CustomerId == 11000);
        jon.AccountNumber.Should().Be("AW00011000");
        jon.FirstName.Should().Be("Jon");
        jon.LastName.Should().Be("Yang");
        jon.City.Should().Be("Rockhampton");
        jon.CountryRegion.Should().Be("Australia");

        // OUTER APPLY must not multiply rows even when a person has several addresses/emails.
        rows.Count(c => c.CustomerId == 11000).Should().Be(1);
    }

    [SkippableFact]
    public async Task GetProducts_ReturnsExpectedSampleRow()
    {
        SkipUnlessEnabled();
        var rows = (await new GetProductsExecutor(CreateFactory()).ExecuteAsync(AllRecords, CancellationToken.None))
            .Cast<ProductDto>().ToList();

        var frame = rows.Single(p => p.ProductId == 680);
        frame.Name.Should().Be("HL Road Frame - Black, 58");
        frame.ProductNumber.Should().Be("FR-R92B-58");
        frame.Category.Should().Be("Components");
        frame.Subcategory.Should().Be("Road Frames");
    }

    [SkippableFact]
    public async Task GetOrders_ReturnsHeaderWithNestedDetails()
    {
        SkipUnlessEnabled();
        var rows = (await new GetOrdersExecutor(CreateFactory()).ExecuteAsync(AllRecords, CancellationToken.None))
            .Cast<OrderDto>().ToList();

        var order = rows.Single(o => o.SalesOrderId == 43659);
        order.CustomerName.Should().Be("James Hendergart");
        order.AccountNumber.Should().Be("AW00029825");
        order.TotalDue.Should().Be(23153.2339m);
        order.OrderDetails.Should().HaveCount(12);
        order.OrderDetails.Should().Contain(d => d.ProductNumber == "BK-M82B-42" && d.Quantity == 1);
    }

    [SkippableFact]
    public async Task GetProductInventory_ReturnsRowsForKnownProduct()
    {
        SkipUnlessEnabled();
        var rows = (await new GetProductInventoryExecutor(CreateFactory()).ExecuteAsync(AllRecords, CancellationToken.None))
            .Cast<ProductInventoryDto>().ToList();

        rows.Should().Contain(r => r.ProductId == 1 && r.ProductName == "Adjustable Race" && r.LocationName == "Tool Crib");
    }
}
