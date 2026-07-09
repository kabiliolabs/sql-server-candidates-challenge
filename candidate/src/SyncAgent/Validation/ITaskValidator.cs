using SyncAgent.Platform.Contracts;

namespace SyncAgent.Validation;

/// <summary>Validates a task received from the platform before any of it reaches the database.</summary>
public interface ITaskValidator
{
    TaskValidationResult Validate(SyncTask? task);
}
