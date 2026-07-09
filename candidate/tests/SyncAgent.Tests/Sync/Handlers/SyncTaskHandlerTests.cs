using FluentAssertions;
using SyncAgent.Data;
using SyncAgent.Data.Queries;
using SyncAgent.Dto;
using SyncAgent.Sync;
using SyncAgent.Sync.Handlers;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Sync.Handlers;

public class SyncTaskHandlerTests
{
    private static readonly SyncParameters Parameters =
        new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), MaxRecords: 5000);

    private readonly RecordingSqlQueryExecutor _executor = new();

    public static TheoryData<string, string> HandlerTaskTypesAndQueries => new()
    {
        { "GetCustomers", SyncQueries.GetCustomers },
        { "GetProducts", SyncQueries.GetProducts },
        { "GetOrders", SyncQueries.GetOrders },
        { "GetProductInventory", SyncQueries.GetProductInventory },
    };

    private ISyncTaskHandler CreateHandler(string taskType) => taskType switch
    {
        "GetCustomers" => new GetCustomersHandler(_executor),
        "GetProducts" => new GetProductsHandler(_executor),
        "GetOrders" => new GetOrdersHandler(_executor),
        "GetProductInventory" => new GetProductInventoryHandler(_executor),
        _ => throw new ArgumentOutOfRangeException(nameof(taskType)),
    };

    // The task type a handler declares is what the dispatcher keys its routing table on, and the
    // platform sends these four strings exactly.
    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public void Each_handler_declares_the_task_type_the_platform_sends(string taskType, string _)
    {
        CreateHandler(taskType).TaskType.Should().Be(taskType);
    }

    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public async Task Each_handler_runs_its_own_query_constant(string taskType, string expectedSql)
    {
        await CreateHandler(taskType).ExecuteAsync(Parameters, CancellationToken.None);

        _executor.CallCount.Should().Be(1);
        _executor.LastSql.Should().BeSameAs(expectedSql, "the SQL must be a constant, never composed at run time");
    }

    // modifiedSince and maxRecords reach SQL Server only as bound DbParameters. Nothing is
    // interpolated, so there is no path by which task input could become SQL.
    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public async Task Each_handler_binds_modifiedSince_and_maxRecords_as_parameters(string taskType, string _)
    {
        await CreateHandler(taskType).ExecuteAsync(Parameters, CancellationToken.None);

        _executor.LastParameters.Should().Be(new SqlQueryParameters(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 5000));
    }

    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public async Task Each_handler_threads_the_cancellation_token_to_the_query(string taskType, string _)
    {
        using var cts = new CancellationTokenSource();

        await CreateHandler(taskType).ExecuteAsync(Parameters, cts.Token);

        _executor.LastCancellationToken.Should().Be(cts.Token);
    }

    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public async Task Each_handler_rejects_null_parameters(string taskType, string _)
    {
        var handler = CreateHandler(taskType);

        await FluentActions.Awaiting(() => handler.ExecuteAsync(null!, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(HandlerTaskTypesAndQueries))]
    public void Each_handler_rejects_a_null_query_executor(string taskType, string _)
    {
        _ = taskType;

        FluentActions.Invoking(() => new GetCustomersHandler(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new GetProductsHandler(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new GetOrdersHandler(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new GetProductInventoryHandler(null!)).Should().Throw<ArgumentNullException>();
    }

    // An offset instant must be converted, not relabelled: the same moment, expressed in UTC.
    [Fact]
    public async Task A_modifiedSince_with_an_offset_is_bound_as_the_equivalent_UTC_instant()
    {
        var parameters = new SyncParameters(new DateTimeOffset(2025, 1, 1, 2, 0, 0, TimeSpan.FromHours(2)), 100);

        await new GetProductsHandler(_executor).ExecuteAsync(parameters, CancellationToken.None);

        _executor.LastParameters!.ModifiedSince.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _executor.LastParameters.ModifiedSince.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task A_projection_handler_returns_the_rows_the_query_produced()
    {
        var rows = new List<ProductDto> { new() { ProductId = 680, Name = "HL Road Frame" } };
        var handler = new GetProductsHandler(new RecordingSqlQueryExecutor(rows));

        var result = await handler.ExecuteAsync(Parameters, CancellationToken.None);

        result.Should().ContainSingle().Which.Should().BeSameAs(rows[0]);
    }

    // recordCount is derived from this list, and MaxRecords caps orders. Returning detail lines
    // here would report 40,370 records for 12,304 synced orders.
    [Fact]
    public async Task GetOrders_returns_one_record_per_order_not_per_line_item()
    {
        var rows = new List<OrderRow>
        {
            OrderRowFor(43659, "Mountain-100 Black, 42"),
            OrderRowFor(43659, "Sport-100 Helmet, Blue"),
            OrderRowFor(43660, "Road-150 Red, 62"),
        };

        var result = await new GetOrdersHandler(new RecordingSqlQueryExecutor(rows)).ExecuteAsync(Parameters, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Cast<OrderDto>().Select(o => o.SalesOrderId).Should().Equal(43659, 43660);
    }

    [Fact]
    public async Task GetOrders_returns_an_empty_result_when_no_orders_match()
    {
        var result = await new GetOrdersHandler(new RecordingSqlQueryExecutor(new List<OrderRow>()))
            .ExecuteAsync(Parameters, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static OrderRow OrderRowFor(int salesOrderId, string productName) => new()
    {
        SalesOrderId = salesOrderId,
        ProductName = productName,
        AccountNumber = "AW00029825",
    };
}
