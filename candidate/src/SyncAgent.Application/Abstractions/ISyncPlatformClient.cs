namespace SyncAgent.Application.Abstractions;

public record NextTaskResponse(
    string TaskId,
    string TaskType,
    Dictionary<string, string> Parameters,
    DateTime CreatedAt
);

public record ResultSubmissionResponse(
    bool Accepted,
    string TaskId,
    string? Error = null
);

public interface ISyncPlatformClient
{
    Task<NextTaskResponse?> GetNextTaskAsync(CancellationToken cancellationToken);
    Task<ResultSubmissionResponse> SubmitResultAsync(
        string taskId,
        string taskType,
        string status,
        object? data,
        int recordCount,
        DateTime executedAt,
        string? errorMessage,
        CancellationToken cancellationToken);
}
