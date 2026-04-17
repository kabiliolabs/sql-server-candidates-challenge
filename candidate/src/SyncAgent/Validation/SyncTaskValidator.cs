using SyncAgent.Contracts;
using SyncAgent.Domain;

namespace SyncAgent.Validation;

public sealed class SyncTaskValidator : ISyncTaskValidator
{
    public TaskValidationResult Validate(SyncTaskContract task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            return TaskValidationResult.Failure("Missing required field: taskId");
        }

        if (string.IsNullOrWhiteSpace(task.TaskType))
        {
            return TaskValidationResult.Failure("Missing required field: taskType");
        }

        if (!SyncTaskType.All.Contains(task.TaskType))
        {
            return TaskValidationResult.Failure($"Unsupported task type: {task.TaskType}");
        }

        if (task.Parameters is null)
        {
            return TaskValidationResult.Failure("Missing required field: parameters");
        }

        if (task.Parameters.ModifiedSince == default)
        {
            return TaskValidationResult.Failure("Missing or invalid field: parameters.modifiedSince");
        }

        return TaskValidationResult.Success();
    }
}
