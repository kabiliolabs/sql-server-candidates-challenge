using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SyncAgent.Platform;

/// <summary>
/// Writes every <see cref="DateTime"/> as an ISO-8601 instant with a trailing <c>Z</c>.
/// </summary>
/// <remarks>
/// SQL Server's <c>datetime</c> is timezone-naive, so Dapper hands back
/// <see cref="DateTimeKind.Unspecified"/>. Left alone, <c>System.Text.Json</c> would emit
/// "2025-02-07T10:01:36.827" with no offset, and the contract -- and every sample payload -- shows
/// a <c>Z</c>. The agent's rule is that a naive database timestamp *is* UTC, and this is the one
/// place that rule is applied, rather than being sprinkled over five DTO mappings.
/// <para>
/// A value that is already <see cref="DateTimeKind.Local"/> is converted rather than relabelled,
/// so the instant it denotes never changes.
/// </para>
/// </remarks>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string Iso8601Utc = "yyyy-MM-ddTHH:mm:ss.FFFFFFFK";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        writer.WriteStringValue(utc.ToString(Iso8601Utc, CultureInfo.InvariantCulture));
    }
}
