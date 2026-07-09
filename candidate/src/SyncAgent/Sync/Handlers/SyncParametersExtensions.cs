using SyncAgent.Data;

namespace SyncAgent.Sync.Handlers;

internal static class SyncParametersExtensions
{
    /// <summary>
    /// Maps the validated task parameters onto the two values every sync query binds.
    /// <c>ModifiedSince</c> has already been normalised to UTC by the validator, so taking
    /// <see cref="DateTimeOffset.UtcDateTime"/> converts the type without moving the instant.
    /// </summary>
    public static SqlQueryParameters ToSqlParameters(this SyncParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return new SqlQueryParameters(parameters.ModifiedSince.UtcDateTime, parameters.MaxRecords);
    }
}
