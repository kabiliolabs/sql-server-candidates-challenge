using FluentAssertions;
using SyncAgent.Platform.Contracts;

namespace SyncAgent.Tests.Platform;

/// <summary>
/// The platform answers 400 to a result whose <c>status</c> is not exactly "completed" or "failed",
/// or whose <c>taskId</c>/<c>taskType</c> is missing. These tests hold the factories to that contract.
/// </summary>
public class SyncResultTests
{
    private static readonly DateTime ExecutedAt = new(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc);

    [Fact]
    public void Completed_derives_the_record_count_from_the_data()
    {
        var result = SyncResult.Completed("T1", "GetProducts", [new object(), new object(), new object()], ExecutedAt);

        result.Status.Should().Be("completed");
        result.RecordCount.Should().Be(3);
        result.Data.Should().HaveCount(3);
        result.ErrorMessage.Should().BeNull();
        result.ExecutedAt.Should().Be(ExecutedAt);
    }

    [Fact]
    public void Completed_accepts_an_empty_result_set()
    {
        var result = SyncResult.Completed("T1", "GetProducts", [], ExecutedAt);

        result.RecordCount.Should().Be(0);
        result.Data.Should().BeEmpty();
        result.Status.Should().Be("completed");
    }

    [Fact]
    public void Failed_carries_no_data_and_a_zero_record_count()
    {
        var result = SyncResult.Failed("T1", "GetOrders", "Database query failed.", ExecutedAt);

        result.Status.Should().Be("failed");
        result.Data.Should().BeNull();
        result.RecordCount.Should().Be(0);
        result.ErrorMessage.Should().Be("Database query failed.");
    }

    [Theory]
    [InlineData(null, "GetProducts")]
    [InlineData("", "GetProducts")]
    [InlineData("  ", "GetProducts")]
    [InlineData("T1", null)]
    [InlineData("T1", "")]
    public void Completed_rejects_the_identifiers_the_platform_would_reject(string? taskId, string? taskType)
    {
        FluentActions.Invoking(() => SyncResult.Completed(taskId!, taskType!, [], ExecutedAt))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null, "GetProducts", "boom")]
    [InlineData("T1", null, "boom")]
    [InlineData("T1", "GetProducts", null)]
    [InlineData("T1", "GetProducts", "")]
    public void Failed_rejects_a_missing_identifier_or_an_empty_error_message(string? taskId, string? taskType, string? errorMessage)
    {
        FluentActions.Invoking(() => SyncResult.Failed(taskId!, taskType!, errorMessage!, ExecutedAt))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Completed_rejects_null_data()
    {
        FluentActions.Invoking(() => SyncResult.Completed("T1", "GetProducts", null!, ExecutedAt))
            .Should().Throw<ArgumentNullException>();
    }

    // Guards the reason every setter is `private init`: a public init accessor on a record leaves
    // `result with { Status = "bogus" }` open to callers, which would defeat the factories.
    [Fact]
    public void Every_setter_is_private_so_the_factories_are_the_only_way_in()
    {
        typeof(SyncResult).GetProperties()
            .Select(p => p.SetMethod)
            .Should().OnlyContain(setter => setter == null || setter.IsPrivate);

        typeof(SyncResult).GetConstructors().Should().BeEmpty();
    }
}
