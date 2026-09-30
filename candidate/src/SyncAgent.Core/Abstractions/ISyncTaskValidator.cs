using SyncAgent.Core.Models;

namespace SyncAgent.Core.Abstractions
{
    public interface ISyncTaskValidator
    {
        SyncTaskValidationResult Validate(SyncTask task, IReadOnlySet<string> supportedTaskTypes);
    }

    public sealed record SyncTaskValidationResult(bool IsValid, string? Error, SyncQuery? Query)
    {
        public static SyncTaskValidationResult Valid(SyncQuery query) => new(true, null, query);

        public static SyncTaskValidationResult Invalid(string error) => new(false, error, null);
    }
}