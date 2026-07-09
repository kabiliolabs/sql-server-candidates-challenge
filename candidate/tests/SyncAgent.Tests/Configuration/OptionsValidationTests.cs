using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using SyncAgent.Configuration;

namespace SyncAgent.Tests.Configuration;

/// <summary>
/// The options types are validated at startup with <c>ValidateDataAnnotations().ValidateOnStart()</c>,
/// which runs <see cref="Validator.TryValidateObject"/> with <c>validateAllProperties: true</c>.
/// These tests run the same validator directly, so they fail for the same reasons the host would.
/// </summary>
public class OptionsValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static IEnumerable<string> MembersInError(object options) =>
        Validate(options).SelectMany(r => r.MemberNames);

    [Fact]
    public void AgentOptions_defaults_are_valid()
    {
        Validate(new AgentOptions()).Should().BeEmpty();
    }

    [Fact]
    public void PlatformOptions_defaults_are_valid_once_an_api_key_is_supplied()
    {
        Validate(new PlatformOptions { ApiKey = "k" }).Should().BeEmpty();
    }

    // The cap is the agent's only defence against a multi-megabyte POST body: an uncapped
    // GetOrders returns 12,304 orders and 40,370 detail lines.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void AgentOptions_rejects_a_MaxRecords_outside_the_supported_range(int maxRecords)
    {
        MembersInError(new AgentOptions { MaxRecords = maxRecords })
            .Should().Contain(nameof(AgentOptions.MaxRecords));
    }

    [Fact]
    public void AgentOptions_accepts_the_boundaries_of_the_MaxRecords_range()
    {
        Validate(new AgentOptions { MaxRecords = 1 }).Should().BeEmpty();
        Validate(new AgentOptions { MaxRecords = 100_000 }).Should().BeEmpty();
    }

    // A zero poll interval would turn the idle agent into a hot loop against the platform.
    [Fact]
    public void AgentOptions_rejects_a_zero_PollInterval()
    {
        MembersInError(new AgentOptions { PollInterval = TimeSpan.Zero })
            .Should().Contain(nameof(AgentOptions.PollInterval));
    }

    [Fact]
    public void AgentOptions_rejects_a_PollInterval_beyond_an_hour()
    {
        MembersInError(new AgentOptions { PollInterval = TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1) })
            .Should().Contain(nameof(AgentOptions.PollInterval));
    }

    [Fact]
    public void AgentOptions_rejects_a_non_positive_CommandTimeout()
    {
        MembersInError(new AgentOptions { CommandTimeoutSeconds = 0 })
            .Should().Contain(nameof(AgentOptions.CommandTimeoutSeconds));
    }

    // The API key is a secret with no safe default. Starting without one must fail loudly at
    // startup rather than producing a 401 on the first poll.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PlatformOptions_rejects_a_blank_ApiKey(string apiKey)
    {
        MembersInError(new PlatformOptions { ApiKey = apiKey })
            .Should().Contain(nameof(PlatformOptions.ApiKey));
    }

    // Transport pinning: the agent must not be configurable to talk to anything but an http(s)
    // origin. `[Required]` could not express this -- BaseAddress has a non-null default and so
    // would always pass -- which is why PlatformOptions implements IValidatableObject.
    [Theory]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("ftp://example.test")]
    public void PlatformOptions_rejects_a_BaseAddress_that_is_not_http_or_https(string baseAddress)
    {
        MembersInError(new PlatformOptions { ApiKey = "k", BaseAddress = new Uri(baseAddress) })
            .Should().Contain(nameof(PlatformOptions.BaseAddress));
    }

    [Fact]
    public void PlatformOptions_rejects_a_relative_BaseAddress()
    {
        var options = new PlatformOptions { ApiKey = "k", BaseAddress = new Uri("/api", UriKind.Relative) };

        MembersInError(options).Should().Contain(nameof(PlatformOptions.BaseAddress));
    }

    [Theory]
    [InlineData("http://localhost:5100")]
    [InlineData("https://platform.example.test")]
    public void PlatformOptions_accepts_an_http_or_https_BaseAddress(string baseAddress)
    {
        Validate(new PlatformOptions { ApiKey = "k", BaseAddress = new Uri(baseAddress) }).Should().BeEmpty();
    }

    [Fact]
    public void PlatformOptions_rejects_a_zero_HttpTimeout()
    {
        MembersInError(new PlatformOptions { ApiKey = "k", HttpTimeout = TimeSpan.Zero })
            .Should().Contain(nameof(PlatformOptions.HttpTimeout));
    }
}
