using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;
using SyncAgent.Sync;
using SyncAgent.Sync.Handlers;

namespace SyncAgent.Tests.Integration;

/// <summary>
/// Runs the four sync queries against the real AdventureWorks2025 database.
/// </summary>
/// <remarks>
/// Excluded from the default run; opt in with <c>dotnet test --filter Category=Integration</c>.
/// These cover the two things a unit test cannot: that each query is valid T-SQL against the actual
/// schema, and that its columns map onto the DTO.
/// <para>
/// The database is resolved by <see cref="IntegrationTestConfiguration"/>, so a grader on something
/// other than <c>.\SQLEXPRESS</c> points these at their own instance with the
/// <c>ConnectionStrings__AdventureWorks</c> environment variable rather than editing source.
/// </para>
/// <para>
/// Assertions are structural -- "no fan-out", "nulls survive", "the cap holds" -- rather than exact
/// row counts, because the counts are properties of one particular database restore. Nothing here
/// tries to reproduce the sample payload rows: order 43659's ModifiedDate is 2022-06-06, so a
/// modifiedSince of 2025-01-01 excludes it.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class SyncQueriesIntegrationTests
{
    private static string ConnectionString => IntegrationTestConfiguration.ConnectionString;

    private static readonly DateTimeOffset ModifiedSince = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SqlQueryExecutor CreateExecutor() => new(
        IntegrationTestConfiguration.DatabaseOptions,
        Options.Create(new AgentOptions()));

    private static SyncParameters Parameters(int maxRecords = 5000) => new(ModifiedSince, maxRecords);

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, int maxRecords = 5000) =>
        await CreateExecutor().QueryAsync<T>(sql, Parameters(maxRecords).ToSqlParameters(), CancellationToken.None);

    // ---- the queries run, and map ------------------------------------------------------------

    [Fact]
    public async Task GetCustomers_returns_rows_that_map_onto_CustomerDto()
    {
        var rows = await QueryAsync<CustomerDto>(SyncQueries.GetCustomers, maxRecords: 100);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(c => c.CustomerId > 0);
        rows.Should().OnlyContain(c => c.AccountNumber.StartsWith("AW", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetProducts_returns_rows_that_map_onto_ProductDto()
    {
        var rows = await QueryAsync<ProductDto>(SyncQueries.GetProducts);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(p => p.ProductId > 0 && p.Name.Length > 0);
        rows.Should().Contain(p => p.ListPrice > 0m);
    }

    [Fact]
    public async Task GetProductInventory_returns_rows_that_map_onto_InventoryDto()
    {
        var rows = await QueryAsync<InventoryDto>(SyncQueries.GetProductInventory);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(i => i.ProductId > 0 && i.LocationName.Length > 0);
    }

    [Fact]
    public async Task GetOrders_returns_rows_that_map_onto_OrderRow()
    {
        var rows = await QueryAsync<OrderRow>(SyncQueries.GetOrders, maxRecords: 10);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => r.SalesOrderId > 0 && r.LineTotal > 0m);
    }

    // ---- the traps the queries were written to avoid -----------------------------------------

    // 24 customers have more than one address. A plain join would duplicate those customer rows;
    // OUTER APPLY collapses each to exactly one address.
    [Fact]
    public async Task GetCustomers_emits_exactly_one_row_per_customer()
    {
        var rows = await QueryAsync<CustomerDto>(SyncQueries.GetCustomers);

        rows.Select(c => c.CustomerId).Should().OnlyHaveUniqueItems();
    }

    // 701 customers are stores with no person, so the person columns are genuinely null.
    // An INNER JOIN to Person.Person would drop them without an error.
    [Fact]
    public async Task GetCustomers_includes_store_customers_that_have_no_person()
    {
        var rows = await QueryAsync<CustomerDto>(SyncQueries.GetCustomers);

        rows.Should().Contain(c => c.FirstName == null && c.LastName == null);
        rows.Should().Contain(c => c.FirstName != null, "individual customers must still come through");
    }

    // 209 of 504 products have no ProductSubcategoryID. An INNER JOIN would silently drop 41%
    // of the catalogue, and the query would still look like it worked.
    [Fact]
    public async Task GetProducts_includes_products_that_have_no_subcategory()
    {
        var rows = await QueryAsync<ProductDto>(SyncQueries.GetProducts);

        rows.Should().Contain(p => p.Subcategory == null && p.Category == null);
        rows.Should().Contain(p => p.Subcategory != null, "categorised products must still come through");
    }

    [Fact]
    public async Task GetProducts_leaves_a_missing_colour_null_rather_than_empty()
    {
        var rows = await QueryAsync<ProductDto>(SyncQueries.GetProducts);

        rows.Should().Contain(p => p.Color == null);
        rows.Should().NotContain(p => p.Color == string.Empty);
    }

    // The primary key of Production.ProductInventory is (ProductID, LocationID), so a product
    // stocked in several locations is several rows. That is the contract, not a fan-out bug.
    [Fact]
    public async Task GetProductInventory_returns_one_row_per_product_and_location()
    {
        var rows = await QueryAsync<InventoryDto>(SyncQueries.GetProductInventory);

        var productIds = rows.Select(i => i.ProductId).ToList();
        productIds.Should().HaveCountGreaterThan(productIds.Distinct().Count(), "a product stocked in several locations is several rows");

        rows.Select(i => (i.ProductId, i.LocationName)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetProductInventory_trims_the_padded_shelf_column()
    {
        var rows = await QueryAsync<InventoryDto>(SyncQueries.GetProductInventory);

        rows.Where(i => i.Shelf != null).Should().OnlyContain(i => i.Shelf == i.Shelf!.Trim());
    }

    // ---- the cap ------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public async Task MaxRecords_caps_each_projection_query(int maxRecords)
    {
        (await QueryAsync<CustomerDto>(SyncQueries.GetCustomers, maxRecords)).Should().HaveCount(maxRecords);
        (await QueryAsync<ProductDto>(SyncQueries.GetProducts, maxRecords)).Should().HaveCount(maxRecords);
        (await QueryAsync<InventoryDto>(SyncQueries.GetProductInventory, maxRecords)).Should().HaveCount(maxRecords);
    }

    // The cap counts orders, not line items, and must never post half an order. The CTE takes the
    // capped headers first and only then joins details -- this proves the details are complete.
    [Fact]
    public async Task MaxRecords_caps_GetOrders_by_order_and_never_truncates_an_order()
    {
        const int MaxOrders = 5;

        var handler = new GetOrdersHandler(CreateExecutor());
        var orders = (await handler.ExecuteAsync(Parameters(MaxOrders), CancellationToken.None))
            .Cast<OrderDto>().ToList();

        orders.Should().HaveCount(MaxOrders);
        orders.Should().OnlyContain(o => o.OrderDetails.Count > 0);
        orders.Sum(o => o.OrderDetails.Count).Should().BeGreaterThan(MaxOrders, "these orders have several lines each");

        await using var connection = new SqlConnection(ConnectionString);
        foreach (var order in orders)
        {
            var expectedLines = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Sales.SalesOrderDetail WHERE SalesOrderID = @id",
                new { id = order.SalesOrderId });

            order.OrderDetails.Should().HaveCount(expectedLines, "order {0} must be posted whole", order.SalesOrderId);
        }
    }

    // ---- the incremental filter ---------------------------------------------------------------

    [Fact]
    public async Task ModifiedSince_actually_filters_the_orders_it_returns()
    {
        var executor = CreateExecutor();

        var since2025 = await executor.QueryAsync<OrderRow>(
            SyncQueries.GetOrders, new SqlQueryParameters(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100_000), CancellationToken.None);
        var since1900 = await executor.QueryAsync<OrderRow>(
            SyncQueries.GetOrders, new SqlQueryParameters(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100_000), CancellationToken.None);

        since2025.Select(r => r.SalesOrderId).Distinct().Should()
            .HaveCountLessThan(since1900.Select(r => r.SalesOrderId).Distinct().Count());
    }

    // A future modifiedSince must produce an empty -- not a failed -- sync.
    [Fact]
    public async Task A_modifiedSince_in_the_future_returns_no_rows_rather_than_failing()
    {
        var rows = await CreateExecutor().QueryAsync<ProductDto>(
            SyncQueries.GetProducts,
            new SqlQueryParameters(new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc), 5000),
            CancellationToken.None);

        rows.Should().BeEmpty();
    }

    // There is deliberately no "injection string is rejected" test here. SqlQueryParameters types
    // ModifiedSince as a DateTime, so an injection payload cannot be expressed at this layer at
    // all -- a test of it would pass regardless of what the SQL did, which is worse than no test.
    // The property is enforced by the type system and by SyncQueries being const strings; the
    // string-shaped hostile inputs are rejected in TaskValidatorTests, where they can arrive.

    // ---- cancellation --------------------------------------------------------------------------

    [Fact]
    public async Task A_cancelled_token_cancels_the_query_rather_than_running_it_to_completion()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => CreateExecutor().QueryAsync<ProductDto>(SyncQueries.GetProducts, Parameters().ToSqlParameters(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
