namespace SyncAgent.Core.Models
{
    public sealed record SyncTask(string TaskId, string TaskType, SyncParameters? Parameters, DateTime CreatedAt);

    public sealed record SyncParameters(DateTime? ModifiedSince);

    public sealed record SyncQuery(DateTime ModifiedSinceUtc);
}