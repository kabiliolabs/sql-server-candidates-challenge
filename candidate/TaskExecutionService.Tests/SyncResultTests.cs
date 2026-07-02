using FluentAssertions;
using TaskExecutionService.Platform;
using Xunit;

namespace TaskExecutionService.Tests;

public sealed class SyncResultTests
{
    private static readonly DateTime ExecutedAt = new(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc);

    [Fact]
    public void Completed_SetsRecordCountToDataLength_AndNullError()
    {
        var data = new object[] { new { a = 1 }, new { a = 2 }, new { a = 3 } };

        var result = SyncResult.Completed("id", "GetProducts", data, ExecutedAt);

        result.Status.Should().Be("completed");
        result.Data.Should().BeSameAs(data);
        result.RecordCount.Should().Be(3);
        result.ErrorMessage.Should().BeNull();
        result.ExecutedAt.Should().Be(ExecutedAt);
    }

    [Fact]
    public void Failed_HasNullData_ZeroCount_AndMessage()
    {
        var result = SyncResult.Failed("id", "GetProducts", "something went wrong", ExecutedAt);

        result.Status.Should().Be("failed");
        result.Data.Should().BeNull();
        result.RecordCount.Should().Be(0);
        result.ErrorMessage.Should().Be("something went wrong");
    }
}
