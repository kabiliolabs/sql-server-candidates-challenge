using System.Text.Json;

namespace SyncAgent.Platform;

/// <summary>
/// The single JSON contract shared by request and response handling.
/// </summary>
/// <remarks>
/// <see cref="JsonSerializerDefaults.Web"/> already means camelCase property names and
/// case-insensitive reads, which is exactly the documented contract: the agent emits camelCase,
/// and keeps working if the platform ever answers in PascalCase.
/// <para>
/// The result's <c>data</c> is declared as <c>IReadOnlyList&lt;object&gt;</c>. Because the element
/// type is exactly <c>object</c>, <c>System.Text.Json</c> serialises each element by its runtime
/// type, so a handler's DTO is written out in full rather than as <c>{}</c>. That behaviour is what
/// lets one dispatcher return rows for four unrelated task types, and it is pinned by a test.
/// </para>
/// </remarks>
internal static class PlatformSerialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new UtcDateTimeJsonConverter() },
    };
}
