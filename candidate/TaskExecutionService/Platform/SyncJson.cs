using System.Text.Json;

namespace TaskExecutionService.Platform;

/// <summary>
/// Single source of truth for JSON (de)serialization against the platform.
/// The platform's models use camelCase property names, so we mirror that with a
/// camelCase policy and case-insensitive reads for robustness.
/// </summary>
public static class SyncJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // errorMessage must be emitted as null when completed, so do NOT ignore nulls.
    };
}
