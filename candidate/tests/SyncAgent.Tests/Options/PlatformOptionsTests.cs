using Shouldly;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Tests.Options;

public sealed class PlatformOptionsTests
{
    private static PlatformOptions Valid(
        string baseUrl = "http://localhost:5100",
        string apiKey = "candidate-test-key-2026",
        int pollingIntervalSeconds = 5,
        int maxBackoffSeconds = 60)
        => new()
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            PollingIntervalSeconds = pollingIntervalSeconds,
            RequestTimeoutSeconds = 30,
            MaxBackoffSeconds = maxBackoffSeconds
        };

    [Theory]
    [InlineData("http://localhost:5100")]
    [InlineData("http://127.0.0.1:5100")]
    [InlineData("https://platform.example.com")]
    public void Valid_configuration_passes(string baseUrl)
        => OptionsValidation.Validate(Valid(baseUrl: baseUrl)).ShouldBeEmpty();

    [Fact]
    public void Plain_http_to_a_remote_host_is_rejected()
    {
        var errors = OptionsValidation.Validate(Valid(baseUrl: "http://platform.example.com"));

        var error = errors.ShouldHaveSingleItem();
        error.ErrorMessage.ShouldBe("BaseUrl must use HTTPS for non-local hosts.");
        error.MemberNames.ShouldContain(nameof(PlatformOptions.BaseUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Missing_or_too_short_api_key_is_rejected(string apiKey)
    {
        var errors = OptionsValidation.Validate(Valid(apiKey: apiKey));

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(PlatformOptions.ApiKey)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    public void Missing_or_invalid_base_url_is_rejected(string baseUrl)
    {
        var errors = OptionsValidation.Validate(Valid(baseUrl: baseUrl));

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(PlatformOptions.BaseUrl)));
    }

    [Fact]
    public void Max_backoff_lower_than_polling_interval_is_rejected()
    {
        var errors = OptionsValidation.Validate(Valid(pollingIntervalSeconds: 30, maxBackoffSeconds: 10));

        errors.ShouldHaveSingleItem().MemberNames.ShouldContain(nameof(PlatformOptions.MaxBackoffSeconds));
    }

    [Fact]
    public void Out_of_range_polling_interval_is_rejected()
    {
        var errors = OptionsValidation.Validate(Valid(pollingIntervalSeconds: 0));

        errors.ShouldContain(e => e.MemberNames.Contains(nameof(PlatformOptions.PollingIntervalSeconds)));
    }
}
