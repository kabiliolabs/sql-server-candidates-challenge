using Microsoft.Extensions.Options;

namespace SyncAgent.Configuration;

/// <summary>
/// Validates <see cref="SyncAgentOptions"/> at startup so misconfiguration fails fast
/// instead of surfacing as a confusing runtime error on the first poll cycle.
/// </summary>
public sealed class SyncAgentOptionsValidator : IValidateOptions<SyncAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, SyncAgentOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            errors.Add($"{nameof(SyncAgentOptions.ApiBaseUrl)} is required.");
        }
        else if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            errors.Add($"{nameof(SyncAgentOptions.ApiBaseUrl)} must be a valid absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add($"{nameof(SyncAgentOptions.ApiKey)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            errors.Add($"{nameof(SyncAgentOptions.ConnectionString)} is required.");
        }

        if (options.PollIntervalSeconds <= 0)
        {
            errors.Add($"{nameof(SyncAgentOptions.PollIntervalSeconds)} must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
