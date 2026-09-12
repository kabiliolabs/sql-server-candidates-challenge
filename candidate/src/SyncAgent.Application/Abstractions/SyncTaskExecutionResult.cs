namespace SyncAgent.Application.Abstractions;

/// <summary>
/// Outcome of executing a single sync task. Created through <see cref="Completed"/> or
/// <see cref="Failed"/> so the platform contract's rule that a failed result carries no data
/// and a zero record count cannot be violated by a caller.
/// </summary>
public sealed record SyncTaskExecutionResult
{
    private SyncTaskExecutionResult(bool success, object? data, int recordCount, string? errorMessage)
    {
        Success = success;
        Data = data;
        RecordCount = recordCount;
        ErrorMessage = errorMessage;
    }

    public bool Success { get; }
    public object? Data { get; }
    public int RecordCount { get; }
    public string? ErrorMessage { get; }

    public static SyncTaskExecutionResult Completed<TRecord>(IReadOnlyList<TRecord> records) =>
        new(success: true, data: records, recordCount: records.Count, errorMessage: null);

    public static SyncTaskExecutionResult Failed(string errorMessage) =>
        new(success: false, data: null, recordCount: 0, errorMessage: errorMessage);
}
