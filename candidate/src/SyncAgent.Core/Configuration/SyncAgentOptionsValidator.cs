using Microsoft.Extensions.Options;

namespace SyncAgent.Core.Configuration;

public sealed class SyncAgentOptionsValidator : IValidateOptions<SyncAgentOptions>
{
    public ValidateOptionsResult Validate(string? name, SyncAgentOptions options)
    {
        var failures = new List<string>();

        if (options.MaxRowsPerTask <= 0)
        {
            failures.Add($"{nameof(SyncAgentOptions.MaxRowsPerTask)} must be greater than zero.");
        }

        if (options.PollIntervalSeconds <= 0)
        {
            failures.Add($"{nameof(SyncAgentOptions.PollIntervalSeconds)} must be greater than zero.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
