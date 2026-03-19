using SyncAgent.Contracts;

namespace SyncAgent.Validation;

public interface ISyncTaskValidator
{
    TaskValidationResult Validate(SyncTaskContract task);
}

public sealed class TaskValidationResult
{
    private TaskValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public static TaskValidationResult Success() => new(true, null);
    public static TaskValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
