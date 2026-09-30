using System.Text.Json;
using Shouldly;
using SyncAgent.Infrastructure.Platform;

namespace SyncAgent.Tests.Platform;

public sealed class UtcDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = PlatformClient.JsonOptions;

    [Fact]
    public void Unspecified_kind_is_written_as_utc_with_z_suffix()
    {
        var value = new DateTime(2025, 8, 7, 0, 0, 0, DateTimeKind.Unspecified);

        JsonSerializer.Serialize(value, Options).ShouldBe("\"2025-08-07T00:00:00Z\"");
    }

    [Fact]
    public void Utc_value_is_written_unchanged()
    {
        var value = new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Utc);

        JsonSerializer.Serialize(value, Options).ShouldBe("\"2025-02-07T10:01:36.827Z\"");
    }

    [Fact]
    public void Local_value_is_converted_to_utc()
    {
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        JsonSerializer.Serialize(utc.ToLocalTime(), Options).ShouldBe("\"2025-01-01T12:00:00Z\"");
    }

    [Fact]
    public void Utc_string_is_read_as_utc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2025-01-01T00:00:00Z\"", Options);

        value.ShouldBe(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void String_with_offset_is_normalized_to_utc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2025-01-01T00:00:00+02:00\"", Options);

        value.ShouldBe(new DateTime(2024, 12, 31, 22, 0, 0, DateTimeKind.Utc));
        value.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
