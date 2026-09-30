using Shouldly;
using SyncAgent.Core.Models;
using SyncAgent.Core.Validation;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Validation;

public sealed class SyncTaskValidatorTests
{
    private static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        SyncTaskTypes.GetCustomers,
        SyncTaskTypes.GetProducts,
        SyncTaskTypes.GetOrders,
        SyncTaskTypes.GetProductInventory
    };

    private readonly SyncTaskValidator _validator = new(new FixedTimeProvider(TestData.Now));

    [Fact]
    public void Valid_task_returns_normalized_query()
    {
        var result = _validator.Validate(TestData.Task(), SupportedTypes);

        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.Query.ShouldNotBeNull();
        result.Query.ModifiedSinceUtc.ShouldBe(TestData.ModifiedSince);
        result.Query.ModifiedSinceUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO-SHORT")]
    [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6CX")] // 27 chars
    [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6I")]  // 'I' is not Crockford base32
    [InlineData("01jqfg8n3xrtv5khw2yp4m7b6c")]  // canonical ULIDs are upper case
    [InlineData("01JQFG8N3XRTV5KHW2YP4M7B6'")]  // injection-like character
    public void Invalid_task_id_is_rejected(string taskId)
    {
        var result = _validator.Validate(TestData.Task(taskId: taskId), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Invalid taskId.");
        result.Query.ShouldBeNull();
    }

    [Fact]
    public void Null_task_id_is_rejected()
    {
        var result = _validator.Validate(TestData.Task(taskId: null!), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Invalid taskId.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("getcustomers")] // task types are case-sensitive, as in the API contract
    [InlineData("GetCustomers; DROP TABLE Sales.Customer")]
    public void Unsupported_task_type_is_rejected_without_echoing_input(string taskType)
    {
        var result = _validator.Validate(TestData.Task(taskType: taskType), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Unsupported taskType.");
    }

    [Fact]
    public void Missing_parameters_are_rejected()
    {
        var result = _validator.Validate(TestData.Task(withParameters: false), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Missing required parameter: modifiedSince.");
    }

    [Fact]
    public void Missing_modified_since_is_rejected()
    {
        var task = TestData.Task() with { Parameters = new SyncParameters(null) };

        var result = _validator.Validate(task, SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Missing required parameter: modifiedSince.");
    }

    [Fact]
    public void Modified_since_before_1900_is_rejected()
    {
        var result = _validator.Validate(
            TestData.Task(modifiedSince: new DateTime(1899, 12, 31, 0, 0, 0, DateTimeKind.Utc)), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Parameter modifiedSince is out of range.");
    }

    [Fact]
    public void Modified_since_in_the_future_is_rejected()
    {
        var result = _validator.Validate(
            TestData.Task(modifiedSince: TestData.Now.UtcDateTime.AddHours(1)), SupportedTypes);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Parameter modifiedSince is out of range.");
    }

    [Fact]
    public void Modified_since_within_clock_skew_is_accepted()
    {
        var result = _validator.Validate(
            TestData.Task(modifiedSince: TestData.Now.UtcDateTime.AddMinutes(1)), SupportedTypes);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Unspecified_kind_is_treated_as_utc()
    {
        var unspecified = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var result = _validator.Validate(TestData.Task(modifiedSince: unspecified), SupportedTypes);

        result.IsValid.ShouldBeTrue();
        result.Query!.ModifiedSinceUtc.Kind.ShouldBe(DateTimeKind.Utc);
        result.Query.ModifiedSinceUtc.Ticks.ShouldBe(unspecified.Ticks);
    }
}
