using Microsoft.Extensions.Options;

namespace SyncAgent.Infrastructure.Platform;

public sealed class PlatformOptionsValidator : IValidateOptions<PlatformOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl) || !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add($"{nameof(PlatformOptions.BaseUrl)} must be a valid absolute URL (set the SYNCAGENT_PLATFORM_URL environment variable).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add($"{nameof(PlatformOptions.ApiKey)} is required (set the SYNCAGENT_API_KEY environment variable).");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
