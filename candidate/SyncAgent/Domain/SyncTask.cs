using System;

namespace SyncAgent.Domain
{
    public sealed class SyncTask
    {
        // JSON property names match the platform contract (camelCase).
        public string taskId;
        public string taskType;
        public TaskParameters parameters;
        public DateTime? createdAt;
    }

    public sealed class TaskParameters
    {
        public DateTime? modifiedSince;
    }
}