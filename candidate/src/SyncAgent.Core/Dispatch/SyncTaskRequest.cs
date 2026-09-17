namespace SyncAgent.Core.Dispatch;

/// <summary>The validated (parsed, well-formed) parameters of a task, extracted from the raw
/// SyncTask.Parameters dictionary by SyncTaskDispatcher before any query runs.</summary>
public sealed record SyncTaskRequest(string TaskId, DateTimeOffset ModifiedSince);
