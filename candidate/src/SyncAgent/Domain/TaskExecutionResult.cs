namespace SyncAgent.Domain;

public sealed class TaskExecutionResult
{
    private TaskExecutionResult(string status, IReadOnlyCollection<object>? data, string? errorMessage)
    {
        Status = status;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public string Status { get; }
    public IReadOnlyCollection<object>? Data { get; }
    public string? ErrorMessage { get; }

    public static TaskExecutionResult Completed(IReadOnlyCollection<object> data) =>
        new("completed", data, null);

    public static TaskExecutionResult Failed(string errorMessage) =>
        new("failed", null, errorMessage);
}
