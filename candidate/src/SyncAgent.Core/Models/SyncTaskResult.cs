using System;
using System.Collections.Generic;

namespace SyncAgent.Core.Models
{
    /// <summary>
    /// The body of <c>POST /api/sync/result</c>.
    /// </summary>
    /// <remarks>
    /// Built only through the two factory methods so the shape can never contradict itself.
    /// The platform does not enforce these invariants — it happily accepts a "completed"
    /// result with null data and a record count that matches nothing — which is exactly why
    /// the agent enforces them on its own side.
    /// </remarks>
    public sealed class SyncTaskResult
    {
        private SyncTaskResult(
            string taskId,
            string taskType,
            string status,
            IReadOnlyList<object>? data,
            int recordCount,
            DateTimeOffset executedAt,
            string? errorMessage)
        {
            TaskId = taskId;
            TaskType = taskType;
            Status = status;
            Data = data;
            RecordCount = recordCount;
            ExecutedAt = executedAt;
            ErrorMessage = errorMessage;
        }

        public string TaskId { get; }

        public string TaskType { get; }

        public string Status { get; }

        public IReadOnlyList<object>? Data { get; }

        public int RecordCount { get; }

        public DateTimeOffset ExecutedAt { get; }

        public string? ErrorMessage { get; }

        public static SyncTaskResult Completed(
            SyncTaskRequest task,
            IReadOnlyList<object> records,
            DateTimeOffset executedAt)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (records is null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            return new SyncTaskResult(
                task.TaskId,
                task.TaskType,
                SyncTaskStatus.Completed,
                records,
                records.Count,
                executedAt,
                errorMessage: null);
        }

        /// <summary>
        /// Builds a failure result. <paramref name="errorMessage"/> travels over the wire, so
        /// callers must hand in an already-sanitised message: never a connection string, SQL
        /// text or stack trace.
        /// </summary>
        public static SyncTaskResult Failed(
            SyncTaskRequest task,
            string errorMessage,
            DateTimeOffset executedAt)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException(
                    "A failed result must carry an error message.",
                    nameof(errorMessage));
            }

            return new SyncTaskResult(
                task.TaskId,
                task.TaskType,
                SyncTaskStatus.Failed,
                data: null,
                recordCount: 0,
                executedAt,
                errorMessage);
        }
    }
}
