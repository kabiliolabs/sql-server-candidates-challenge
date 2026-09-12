using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Application.Abstractions;
using SyncAgent.Application.Services;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Infrastructure.Sql;
using Xunit;

namespace SyncAgent.Infrastructure.IntegrationTests;

// Runs each task type through its real handler and real repository against the live
// AdventureWorks2025 database, then serializes the result exactly as the HTTP client does.
// This covers everything between receiving a task and the body that would be POSTed back:
// parameter validation, SQL, mapping and the JSON field names the platform expects.
public class SyncPipelineTests
{
    private static readonly DatabaseOptions Options = new()
    {
        ConnectionString = "Server=localhost\\SQLEXPRESS;Database=AdventureWorks2025;Trusted_Connection=True;TrustServerCertificate=True;"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static Dictionary<string, string> Parameters(string modifiedSince) =>
        new() { ["modifiedSince"] = modifiedSince };

    private static async Task<JsonElement> ExecuteAndSerializeAsync(ISyncTaskHandler handler)
    {
        var result = await handler.ExecuteAsync(Parameters("2000-01-01T00:00:00Z"), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.RecordCount > 0);

        var json = JsonSerializer.Serialize(result.Data, SerializerOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    private static void AssertHasExactly(JsonElement record, params string[] expectedFieldNames)
    {
        var actual = record.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(expectedFieldNames.OrderBy(n => n).ToArray(), actual);
    }

    // The contract writes every timestamp as ISO-8601 UTC, e.g. "2025-08-07T00:00:00Z".
    private static void AssertIsUtcTimestamp(JsonElement timestamp)
    {
        var text = timestamp.GetString();
        Assert.NotNull(text);
        Assert.EndsWith("Z", text);
    }

    [Fact]
    public async Task GetCustomers_ProducesRecordsWithTheContractFields()
    {
        var handler = new GetCustomersHandler(
            new CustomerRepository(Options.ToOptionsWrapper(), NullLogger<CustomerRepository>.Instance));

        var data = await ExecuteAndSerializeAsync(handler);

        AssertHasExactly(data[0],
            "customerId", "accountNumber", "firstName", "lastName", "emailAddress", "phone",
            "addressLine1", "city", "stateProvince", "postalCode", "countryRegion");
    }

    [Fact]
    public async Task GetProducts_ProducesRecordsWithTheContractFields()
    {
        var handler = new GetProductsHandler(
            new ProductRepository(Options.ToOptionsWrapper(), NullLogger<ProductRepository>.Instance));

        var data = await ExecuteAndSerializeAsync(handler);

        AssertHasExactly(data[0],
            "productId", "name", "productNumber", "color", "standardCost", "listPrice",
            "category", "subcategory", "modifiedDate");

        AssertIsUtcTimestamp(data[0].GetProperty("modifiedDate"));

        // Products without a subcategory must still appear, with the field present and null,
        // because the contract lists it for every record.
        var withoutSubcategory = data.EnumerateArray()
            .First(p => p.GetProperty("subcategory").ValueKind == JsonValueKind.Null);
        Assert.Equal(JsonValueKind.Null, withoutSubcategory.GetProperty("category").ValueKind);
    }

    [Fact]
    public async Task GetOrders_ProducesRecordsWithTheContractFieldsAndNestedLines()
    {
        var handler = new GetOrdersHandler(
            new OrderRepository(Options.ToOptionsWrapper(), NullLogger<OrderRepository>.Instance));

        var data = await ExecuteAndSerializeAsync(handler);

        var order = data.EnumerateArray().First(o => o.GetProperty("salesOrderId").GetInt32() == 43659);
        AssertHasExactly(order,
            "salesOrderId", "orderDate", "status", "customerName", "accountNumber", "totalDue", "orderDetails");

        Assert.Equal("James Hendergart", order.GetProperty("customerName").GetString());
        Assert.Equal("AW00029825", order.GetProperty("accountNumber").GetString());
        AssertIsUtcTimestamp(order.GetProperty("orderDate"));

        var line = order.GetProperty("orderDetails")[0];
        AssertHasExactly(line, "productName", "productNumber", "unitPrice", "quantity", "lineTotal");
    }

    [Fact]
    public async Task GetProductInventory_ProducesRecordsWithTheContractFields()
    {
        var handler = new GetProductInventoryHandler(
            new InventoryRepository(Options.ToOptionsWrapper(), NullLogger<InventoryRepository>.Instance));

        var data = await ExecuteAndSerializeAsync(handler);

        AssertHasExactly(data[0],
            "productId", "productName", "productNumber", "locationName", "shelf", "bin",
            "quantity", "modifiedDate");

        AssertIsUtcTimestamp(data[0].GetProperty("modifiedDate"));
    }

    [Fact]
    public async Task Handlers_WithInvalidModifiedSince_FailWithoutTouchingTheDatabase()
    {
        var handler = new GetCustomersHandler(
            new CustomerRepository(Options.ToOptionsWrapper(), NullLogger<CustomerRepository>.Instance));

        var result = await handler.ExecuteAsync(Parameters("not-a-date"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(0, result.RecordCount);
        Assert.Contains("ISO-8601", result.ErrorMessage);
    }
}
