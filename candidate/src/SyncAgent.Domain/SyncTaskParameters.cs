using System.Globalization;

namespace SyncAgent.Domain;

/// <summary>
/// The parameters carried by a sync task. Values come from the platform, so they are treated as
/// untrusted input and validated before anything downstream uses them.
/// </summary>
public sealed class SyncTaskParameters
{
    private const string ExpectedFormat = "yyyy-MM-ddTHH:mm:ssZ";

    // AdjustToUniversal keeps the parsed value as the literal UTC instant the platform sent.
    // AssumeUniversal on its own shifts it into the host's local time zone, which - against
    // AdventureWorks' offset-less `datetime` columns - would skew the comparison by the host's offset.
    private const DateTimeStyles ParseStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public required string ModifiedSince { get; init; }

    /// <summary>
    /// Returns ModifiedSince as a UTC instant, or throws <see cref="SyncTaskValidationException"/>
    /// when it is missing or not an ISO-8601 UTC timestamp.
    /// </summary>
    public DateTime ParseModifiedSince()
    {
        if (string.IsNullOrWhiteSpace(ModifiedSince))
        {
            throw new SyncTaskValidationException("ModifiedSince parameter is required.");
        }

        if (!DateTime.TryParseExact(ModifiedSince, ExpectedFormat, CultureInfo.InvariantCulture, ParseStyles, out var modifiedSince))
        {
            throw new SyncTaskValidationException(
                $"ModifiedSince must be a valid ISO-8601 UTC datetime (format: {ExpectedFormat}), got: {ModifiedSince}");
        }

        return modifiedSince;
    }
}
