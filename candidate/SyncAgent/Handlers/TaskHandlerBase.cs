using System.Globalization;
using SyncAgent.Contracts;

namespace SyncAgent.Handlers;

/// <summary>
/// Shared base logic for task handlers: defensive modifiedSince parsing, timing,
/// and consistent success/failure SyncResultDto construction (always echoing
/// TaskId/TaskType back from the input task).
/// </summary>
public abstract class TaskHandlerBase(ILogger logger) : ITaskHandler
{
    public abstract string TaskType { get; }

    public async Task<SyncResultDto> HandleAsync(SyncTaskDto task, CancellationToken cancellationToken)
    {
        var executedAt = DateTime.UtcNow;
        try
        {
            var modifiedSince = ParseModifiedSince(task.Parameters);
            var data = await ExecuteAsync(task, modifiedSince, cancellationToken);

            logger.LogInformation(
                "Task {TaskId} of type {TaskType} completed successfully with {RecordCount} record(s).",
                task.TaskId, task.TaskType, data.Count);

            return new SyncResultDto
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "completed",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(data),
                RecordCount = data.Count,
                ExecutedAt = executedAt
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process task {TaskId} of type {TaskType}", task.TaskId, task.TaskType);

            return new SyncResultDto
            {
                TaskId = task.TaskId,
                TaskType = task.TaskType,
                Status = "failed",
                Data = null,
                RecordCount = 0,
                ExecutedAt = executedAt,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Executes the task-specific query and returns the result records.
    /// </summary>
    protected abstract Task<IReadOnlyList<object>> ExecuteAsync(SyncTaskDto task, DateTime modifiedSince, CancellationToken cancellationToken);

    /// <summary>
    /// Defensively parses the "modifiedSince" parameter. Falls back to DateTime.MinValue
    /// (i.e. no filtering) if the parameter is missing, empty, or unparseable, so a
    /// malformed/missing parameter never crashes the handler.
    /// </summary>
    private static DateTime ParseModifiedSince(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("modifiedSince", out var raw)
            && !string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return DateTime.MinValue;
    }
}
