using FluentAssertions;
using TaskExecutionService.Sync;
using Xunit;

namespace TaskExecutionService.Tests;

public sealed class SyncParametersTests
{
    [Fact]
    public void Parse_ValidIsoUtc_ParsesAsUtc()
    {
        var raw = new Dictionary<string, string> { ["modifiedSince"] = "2025-01-01T00:00:00Z" };

        var parameters = SyncParameters.Parse(raw);

        parameters.ModifiedSince.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        parameters.ModifiedSince.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Parse_NullDictionary_Throws()
    {
        var act = () => SyncParameters.Parse(null);

        act.Should().Throw<SyncValidationException>().WithMessage("*modifiedSince*");
    }

    [Fact]
    public void Parse_MissingKey_Throws()
    {
        var raw = new Dictionary<string, string>();

        var act = () => SyncParameters.Parse(raw);

        act.Should().Throw<SyncValidationException>().WithMessage("*modifiedSince*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("2025-13-40")]
    public void Parse_InvalidValue_Throws(string value)
    {
        var raw = new Dictionary<string, string> { ["modifiedSince"] = value };

        var act = () => SyncParameters.Parse(raw);

        act.Should().Throw<SyncValidationException>();
    }
}
