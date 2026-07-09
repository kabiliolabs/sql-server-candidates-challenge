using System.Text.Json;
using FluentAssertions;
using SyncAgent.Dto;
using SyncAgent.Platform;

namespace SyncAgent.Tests.Platform;

/// <summary>
/// Diffs each DTO's serialised field names against the payloads in <c>docs/sample-payloads/</c>,
/// which are copied into the test output and are the closest thing to a written contract.
/// </summary>
/// <remarks>
/// Field <em>names</em> only, never values. The sample rows are shape examples and are not
/// reproducible from the database: <c>result-get-orders.json</c> shows order 43659, whose
/// ModifiedDate is 2022-06-06, so a modifiedSince of 2025-01-01 excludes it.
/// </remarks>
public class ContractShapeTests
{
    private static IReadOnlyList<string> FieldNamesOfFirstDataRow(string sampleFileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SamplePayloads", sampleFileName);
        File.Exists(path).Should().BeTrue($"'{sampleFileName}' should be copied to the test output directory");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [.. document.RootElement.GetProperty("data").EnumerateArray().First().EnumerateObject().Select(p => p.Name)];
    }

    private static IReadOnlyList<string> SerialisedFieldNamesOf<T>(T dto)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dto, PlatformSerialization.Options));
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }

    [Fact]
    public void CustomerDto_matches_the_documented_customer_payload()
    {
        SerialisedFieldNamesOf(new CustomerDto())
            .Should().BeEquivalentTo(FieldNamesOfFirstDataRow("result-get-customers.json"));
    }

    [Fact]
    public void ProductDto_matches_the_documented_product_payload()
    {
        SerialisedFieldNamesOf(new ProductDto())
            .Should().BeEquivalentTo(FieldNamesOfFirstDataRow("result-get-products.json"));
    }

    [Fact]
    public void InventoryDto_matches_the_documented_inventory_payload()
    {
        SerialisedFieldNamesOf(new InventoryDto())
            .Should().BeEquivalentTo(FieldNamesOfFirstDataRow("result-get-product-inventory.json"));
    }

    [Fact]
    public void OrderDto_matches_the_documented_order_payload()
    {
        SerialisedFieldNamesOf(new OrderDto())
            .Should().BeEquivalentTo(FieldNamesOfFirstDataRow("result-get-orders.json"));
    }

    [Fact]
    public void OrderDetailDto_matches_the_nested_orderDetails_of_the_documented_order_payload()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SamplePayloads", "result-get-orders.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var expected = document.RootElement
            .GetProperty("data").EnumerateArray().First()
            .GetProperty("orderDetails").EnumerateArray().First()
            .EnumerateObject().Select(p => p.Name);

        SerialisedFieldNamesOf(new OrderDetailDto()).Should().BeEquivalentTo(expected);
    }

    // Color is nullable in the database and must stay null rather than becoming "".
    [Fact]
    public void A_null_product_colour_serialises_as_null()
    {
        var json = JsonSerializer.Serialize(new ProductDto { Color = null }, PlatformSerialization.Options);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("color").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // Money columns are decimal, never double: 23153.2339 must survive the round trip exactly.
    [Fact]
    public void Money_values_keep_their_precision()
    {
        var json = JsonSerializer.Serialize(new OrderDto { TotalDue = 23153.2339m }, PlatformSerialization.Options);

        json.Should().Contain("23153.2339");
    }

    [Fact]
    public void An_order_serialises_its_line_items_as_a_nested_array()
    {
        var order = new OrderDto
        {
            SalesOrderId = 43659,
            OrderDetails = [new OrderDetailDto { ProductName = "Sport-100 Helmet, Blue", Quantity = 4 }],
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(order, PlatformSerialization.Options));
        var detail = document.RootElement.GetProperty("orderDetails").EnumerateArray().Single();

        detail.GetProperty("productName").GetString().Should().Be("Sport-100 Helmet, Blue");
        detail.GetProperty("quantity").GetInt32().Should().Be(4);
    }
}
