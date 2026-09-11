using System;
using System.Collections.Generic;

namespace SyncAgent.Domain
{
    public sealed class SyncResult
    {
        public string taskId;
        public string taskType;
        public string status; // "completed" | "failed"
        public List<object> data;
        public int recordCount;
        public DateTime executedAt;
        public string errorMessage;

        public static SyncResult Completed(string taskId, string taskType, List<object> data, DateTime executedAt)
        {
            return new SyncResult
            {
                taskId = taskId,
                taskType = taskType,
                status = "completed",
                data = data ?? new List<object>(),
                recordCount = data == null ? 0 : data.Count,
                executedAt = executedAt
            };
        }

        public static SyncResult Failed(string taskId, string taskType, string errorMessage, DateTime executedAt)
        {
            return new SyncResult
            {
                taskId = taskId,
                taskType = taskType,
                status = "failed",
                data = null,
                recordCount = 0,
                executedAt = executedAt,
                errorMessage = errorMessage
            };
        }
    }
}