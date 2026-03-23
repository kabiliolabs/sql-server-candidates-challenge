using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure;

public class SyncTaskValidator : ISyncTaskValidator
{
    public SyncTaskValidationResult Validate(SyncTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (string.IsNullOrWhiteSpace(task.TaskId))
            return SyncTaskValidationResult.Failure("Missing required field: taskId");

        if (string.IsNullOrWhiteSpace(task.TaskType))
            return SyncTaskValidationResult.Failure("Missing required field: taskType");

        if (!task.TryGetModifiedSince(out _))
            return SyncTaskValidationResult.Failure("Invalid parameter: modifiedSince must be a valid date");

        return SyncTaskValidationResult.Success();
    }
}
