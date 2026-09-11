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
    }
}