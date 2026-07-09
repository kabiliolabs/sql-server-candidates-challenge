using System.Text.Json;
using FluentAssertions;
using SyncAgent.Platform;

namespace SyncAgent.Tests.Platform;

/// <summary>
/// SQL Server's <c>datetime</c> is timezone-naive, so Dapper returns
/// <see cref="DateTimeKind.Unspecified"/>. Every sample payload shows a trailing <c>Z</c>.
/// </summary>
public class UtcDateTimeJsonConverterTests
{
    private static string Write(DateTime value) => JsonSerializer.Serialize(value, PlatformSerialization.Options);

    [Fact]
    public void A_naive_database_timestamp_is_written_as_UTC()
    {
        Write(new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Unspecified))
            .Should().Be("\"2025-02-07T10:01:36.827Z\"");
    }

    [Fact]
    public void A_timestamp_that_is_already_UTC_is_written_unchanged()
    {
        Write(new DateTime(2026, 3, 12, 10, 30, 5, DateTimeKind.Utc)).Should().Be("\"2026-03-12T10:30:05Z\"");
    }

    // Relabelling a local time as UTC would move the instant by the machine's offset. Convert it.
    [Fact]
    public void A_local_timestamp_is_converted_rather_than_relabelled()
    {
        var local = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);

        var written = JsonSerializer.Deserialize<DateTime>(Write(local), PlatformSerialization.Options);

        written.Should().Be(local.ToUniversalTime());
    }

    [Fact]
    public void Whole_seconds_are_written_without_a_trailing_fraction()
    {
        Write(new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Unspecified)).Should().Be("\"2022-05-30T00:00:00Z\"");
    }

    [Fact]
    public void A_written_timestamp_reads_back_as_the_same_UTC_instant()
    {
        var original = new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Unspecified);

        var roundTripped = JsonSerializer.Deserialize<DateTime>(Write(original), PlatformSerialization.Options);

        roundTripped.Should().Be(DateTime.SpecifyKind(original, DateTimeKind.Utc));
        roundTripped.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void The_converter_rejects_a_null_writer()
    {
        FluentActions.Invoking(() => new UtcDateTimeJsonConverter().Write(null!, DateTime.UnixEpoch, PlatformSerialization.Options))
            .Should().Throw<ArgumentNullException>();
    }
}
