using Microsoft.Extensions.Options;

namespace SyncAgent.Infrastructure.Data;

public sealed class SqlOptionsValidator : IValidateOptions<SqlOptions>
{
    public ValidateOptionsResult Validate(string? name, SqlOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{nameof(SqlOptions.ConnectionString)} is required (set the SYNCAGENT_DB_CONNECTION environment variable).");
        }

        if (options.CommandTimeoutSeconds <= 0)
        {
            failures.Add($"{nameof(SqlOptions.CommandTimeoutSeconds)} must be greater than zero.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
