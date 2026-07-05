using System.Globalization;

namespace TaskExecutionService.Sync;

/// <summary>
/// Strongly-typed, validated view over a task's raw parameter dictionary.
/// Parsing here keeps validation out of the individual executors and guarantees
/// executors only ever see well-formed input.
/// </summary>
public sealed class SyncParameters
{
    public const string ModifiedSinceKey = "modifiedSince";

    /// <summary>Only records modified on or after this instant (UTC) should be returned.</summary>
    public DateTime ModifiedSince { get; }

    private SyncParameters(DateTime modifiedSince) => ModifiedSince = modifiedSince;

    /// <summary>
    /// Parses and validates the required parameters. Throws
    /// <see cref="SyncValidationException"/> when a required value is missing or malformed.
    /// </summary>
    public static SyncParameters Parse(IReadOnlyDictionary<string, string>? raw)
    {
        if (raw is null || !raw.TryGetValue(ModifiedSinceKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new SyncValidationException($"Missing required parameter '{ModifiedSinceKey}'.");
        }

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var modifiedSince))
        {
            throw new SyncValidationException($"Parameter '{ModifiedSinceKey}' is not a valid ISO-8601 datetime.");
        }

        return new SyncParameters(modifiedSince);
    }
}
