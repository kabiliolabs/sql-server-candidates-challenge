namespace TaskExecutionService.Sync;

/// <summary>
/// Thrown when a task cannot be executed because its type or parameters are
/// invalid. The message is safe to surface back to the platform (it never
/// contains internal/system detail).
/// </summary>
public sealed class SyncValidationException : Exception
{
    public SyncValidationException(string message) : base(message)
    {
    }
}
