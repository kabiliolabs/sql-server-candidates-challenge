using System.Runtime.CompilerServices;
using System.Text.Json;
using SyncAgent.Core.Tasks;
using Xunit;

namespace SyncAgent.Infrastructure.Tests.Contract;

/// <summary>Compares the *shape* (property names) of what our handlers actually produce
/// against docs/sample-payloads/result-*.json — not literal values, since the real database
/// won't match the doc's example rows exactly. Catches accidental field renames/omissions
/// against the documented contract cheaply, without needing a database.</summary>
public class SamplePayloadContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string SamplePayloadsDirectory = ResolveSamplePayloadsDirectory();

    [Fact]
    public void GetCustomers_RecordShape_MatchesSamplePayload()
    {
        var record = new CustomerRecord(11000, "AW00011000", "Jon", "Yang", "jon24@adventure-works.com", null,
            "3761 N. 14th St", "Rockhampton", "Queensland", "4700", "Australia");

        AssertShapeMatchesFirstDataElement(record, "result-get-customers.json");
    }

    [Fact]
    public void GetProducts_RecordShape_MatchesSamplePayload()
    {
        var record = new ProductRecord(680, "HL Road Frame - Black, 58", "FR-R92B-58", "Black", 1059.31m, 1431.50m,
            "Components", "Road Frames", DateTimeOffset.UtcNow);

        AssertShapeMatchesFirstDataElement(record, "result-get-products.json");
    }

    [Fact]
    public void GetProductInventory_RecordShape_MatchesSamplePayload()
    {
        var record = new ProductInventoryRecord(1, "Adjustable Race", "AR-5381", "Tool Crib", "A", 1, 408, DateTimeOffset.UtcNow);

        AssertShapeMatchesFirstDataElement(record, "result-get-product-inventory.json");
    }

    [Fact]
    public void GetOrders_RecordShape_MatchesSamplePayload()
    {
        var record = new OrderRecord(43659, DateTimeOffset.UtcNow, 5, "James Hendergart", "AW00029825", 23153.2339m,
            [new OrderDetailRecord("Mountain-100 Black, 42", "BK-M82B-42", 2024.994m, 1, 2024.994m)]);

        AssertShapeMatchesFirstDataElement(record, "result-get-orders.json");

        // Nested orderDetails shape too — the outer check only covers header-level fields.
        var actualDetailFields = GetPropertyNames(JsonSerializer.SerializeToElement(record.OrderDetails[0], JsonOptions));
        var expectedDetailFields = GetPropertyNames(LoadSampleFirstDataElement("result-get-orders.json").GetProperty("orderDetails")[0]);
        Assert.Equal(expectedDetailFields, actualDetailFields);
    }

    private static void AssertShapeMatchesFirstDataElement<T>(T record, string sampleFileName)
    {
        var actualFields = GetPropertyNames(JsonSerializer.SerializeToElement(record, JsonOptions));
        var expectedFields = GetPropertyNames(LoadSampleFirstDataElement(sampleFileName));
        Assert.Equal(expectedFields, actualFields);
    }

    private static JsonElement LoadSampleFirstDataElement(string sampleFileName)
    {
        var path = Path.Combine(SamplePayloadsDirectory, sampleFileName);
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("data")[0].Clone();
    }

    private static SortedSet<string> GetPropertyNames(JsonElement element) =>
        new(element.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);

    private static string ResolveSamplePayloadsDirectory([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "..", "..", "docs", "sample-payloads"));
}
