using FluentAssertions;
using SyncAgent.Dto;
using SyncAgent.Sync.Handlers;

namespace SyncAgent.Tests.Sync.Handlers;

public class OrderResultBuilderTests
{
    private static readonly DateTime OrderDate = new(2022, 5, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private static OrderRow Row(
        int salesOrderId,
        string productName,
        string? customerName = "James Hendergart",
        decimal lineTotal = 1m,
        int quantity = 1) => new()
        {
            SalesOrderId = salesOrderId,
            OrderDate = OrderDate,
            Status = 5,
            CustomerName = customerName,
            AccountNumber = "AW00029825",
            TotalDue = 23153.2339m,
            ProductName = productName,
            ProductNumber = "BK-M82B-42",
            UnitPrice = 2024.994m,
            Quantity = quantity,
            LineTotal = lineTotal,
        };

    [Fact]
    public void An_order_with_several_lines_becomes_one_record_with_nested_details()
    {
        var orders = OrderResultBuilder.Build(
        [
            Row(43659, "Mountain-100 Black, 42", lineTotal: 2024.994m),
            Row(43659, "Mountain-100 Black, 44", lineTotal: 6074.982m, quantity: 3),
            Row(43659, "Sport-100 Helmet, Blue", lineTotal: 80.746m, quantity: 4),
        ]);

        var order = orders.Should().ContainSingle().Subject;
        order.SalesOrderId.Should().Be(43659);
        order.Status.Should().Be(5);
        order.TotalDue.Should().Be(23153.2339m);
        order.AccountNumber.Should().Be("AW00029825");
        order.OrderDetails.Should().HaveCount(3);
        order.OrderDetails[1].Quantity.Should().Be(3);
        order.OrderDetails[1].LineTotal.Should().Be(6074.982m);
    }

    [Fact]
    public void A_single_line_order_still_nests_its_one_detail()
    {
        var orders = OrderResultBuilder.Build([Row(43659, "Sport-100 Helmet, Blue")]);

        orders.Should().ContainSingle().Which.OrderDetails.Should().ContainSingle()
            .Which.ProductName.Should().Be("Sport-100 Helmet, Blue");
    }

    [Fact]
    public void Two_orders_become_two_records()
    {
        var orders = OrderResultBuilder.Build([Row(43659, "A"), Row(43659, "B"), Row(43660, "C")]);

        orders.Select(o => o.SalesOrderId).Should().Equal(43659, 43660);
        orders[0].OrderDetails.Should().HaveCount(2);
        orders[1].OrderDetails.Should().ContainSingle();
    }

    // The query orders by SalesOrderID, so rows for one order arrive together today. Grouping by
    // key rather than by adjacency means a future change to the ORDER BY cannot quietly start
    // emitting the same order twice.
    [Fact]
    public void Interleaved_rows_for_two_orders_are_still_grouped_into_two_records()
    {
        var orders = OrderResultBuilder.Build([Row(43659, "A"), Row(43660, "B"), Row(43659, "C"), Row(43660, "D")]);

        orders.Should().HaveCount(2);
        orders[0].SalesOrderId.Should().Be(43659);
        orders[0].OrderDetails.Select(d => d.ProductName).Should().Equal("A", "C");
        orders[1].OrderDetails.Select(d => d.ProductName).Should().Equal("B", "D");
    }

    [Fact]
    public void Orders_appear_in_the_order_they_were_first_seen()
    {
        var orders = OrderResultBuilder.Build([Row(43700, "A"), Row(43659, "B"), Row(43680, "C")]);

        orders.Select(o => o.SalesOrderId).Should().Equal(43700, 43659, 43680);
    }

    [Fact]
    public void Line_items_keep_the_order_the_query_returned_them_in()
    {
        var orders = OrderResultBuilder.Build([Row(1, "third"), Row(1, "first"), Row(1, "second")]);

        orders.Single().OrderDetails.Select(d => d.ProductName).Should().Equal("third", "first", "second");
    }

    [Fact]
    public void An_empty_result_set_produces_no_orders()
    {
        OrderResultBuilder.Build([]).Should().BeEmpty();
    }

    // 701 customers are stores with no person. The query's COALESCE falls back to the store name,
    // and the builder must carry whatever that produced through untouched.
    [Fact]
    public void A_store_customers_name_is_carried_through_from_the_query()
    {
        var orders = OrderResultBuilder.Build([Row(43659, "A", customerName: "Better Bike Shop")]);

        orders.Single().CustomerName.Should().Be("Better Bike Shop");
    }

    [Fact]
    public void A_null_customer_name_stays_null()
    {
        OrderResultBuilder.Build([Row(43659, "A", customerName: null)]).Single().CustomerName.Should().BeNull();
    }

    // The header columns repeat on every line of an order; taking the first is only safe because
    // they are identical. Assert the builder does not, say, take the last row's values.
    [Fact]
    public void The_header_of_the_first_row_of_an_order_wins()
    {
        var first = Row(43659, "A") with { TotalDue = 100m };
        var second = Row(43659, "B") with { TotalDue = 999m };

        OrderResultBuilder.Build([first, second]).Single().TotalDue.Should().Be(100m);
    }

    [Fact]
    public void Build_rejects_a_null_row_sequence()
    {
        FluentActions.Invoking(() => OrderResultBuilder.Build(null!)).Should().Throw<ArgumentNullException>();
    }
}
