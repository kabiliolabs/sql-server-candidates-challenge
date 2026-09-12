using SyncAgent.Domain;
using Xunit;

namespace SyncAgent.Domain.Tests;

public class SyncTaskParametersTests
{
    private static SyncTaskParameters WithModifiedSince(string modifiedSince) =>
        new() { ModifiedSince = modifiedSince };

    [Fact]
    public void ParseModifiedSince_PreservesTheExactUtcInstant()
    {
        // The value handed to SqlParameter must be the exact instant the platform sent, whatever
        // time zone the agent runs in, because AdventureWorks compares it against offset-less
        // `datetime` columns.
        var result = WithModifiedSince("2025-01-01T00:00:00Z").ParseModifiedSince();

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ParseModifiedSince_WithNonMidnightTime_PreservesTheExactUtcInstant()
    {
        var result = WithModifiedSince("2025-06-15T13:45:30Z").ParseModifiedSince();

        Assert.Equal(new DateTime(2025, 6, 15, 13, 45, 30, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseModifiedSince_WithMissingValue_ReportsItAsRequired(string modifiedSince)
    {
        var exception = Assert.Throws<SyncTaskValidationException>(
            () => WithModifiedSince(modifiedSince).ParseModifiedSince());

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("2025-01-01")]                 // no time component
    [InlineData("2025-01-01T00:00:00")]         // no UTC designator
    [InlineData("2025-01-01T00:00:00+01:00")]   // an offset instead of UTC
    [InlineData("01/01/2025 00:00:00")]         // a different format entirely
    [InlineData("2025-13-01T00:00:00Z")]        // month out of range
    [InlineData("2025-02-30T00:00:00Z")]        // day that does not exist
    [InlineData("not-a-date")]
    public void ParseModifiedSince_WithMalformedValue_ReportsTheExpectedFormat(string modifiedSince)
    {
        var exception = Assert.Throws<SyncTaskValidationException>(
            () => WithModifiedSince(modifiedSince).ParseModifiedSince());

        Assert.Contains("ISO-8601", exception.Message);
    }
}
