using System.Text.RegularExpressions;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Validation
{
    public sealed partial class SyncTaskValidator(TimeProvider timeProvider) : ISyncTaskValidator
    {
        private static readonly DateTime MinModifiedSince = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

        [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$")]
        private static partial Regex UlidRegex();

        public SyncTaskValidationResult Validate(SyncTask task, IReadOnlySet<string> supportedTaskTypes)
        {
            if (string.IsNullOrWhiteSpace(task.TaskId) || !UlidRegex().IsMatch(task.TaskId))
                return SyncTaskValidationResult.Invalid("Invalid taskId.");

            if (string.IsNullOrWhiteSpace(task.TaskType) || !supportedTaskTypes.Contains(task.TaskType))
                return SyncTaskValidationResult.Invalid("Unsupported taskType.");

            if (task.Parameters?.ModifiedSince is not { } modifiedSince)
                return SyncTaskValidationResult.Invalid("Missing required parameter: modifiedSince.");

            var utc = modifiedSince.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(modifiedSince, DateTimeKind.Utc)
                : modifiedSince.ToUniversalTime();

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (utc < MinModifiedSince || utc > now + AllowedClockSkew)
                return SyncTaskValidationResult.Invalid("Parameter modifiedSince is out of range.");

            return SyncTaskValidationResult.Valid(new SyncQuery(utc));
        }
    }
}