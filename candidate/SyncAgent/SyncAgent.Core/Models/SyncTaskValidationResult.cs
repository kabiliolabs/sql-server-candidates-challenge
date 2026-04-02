namespace SyncAgent.Core.Models;

public class SyncTaskValidationResult
{
    private SyncTaskValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public static SyncTaskValidationResult Success() => new(true, null);

    public static SyncTaskValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
