using SyncAgent.Core.Models;

namespace SyncAgent.Tests.TestDoubles;

internal static class TestData
{
    public const string ValidTaskId = "01JQFG8N3XRTV5KHW2YP4M7B6C";

    public static readonly DateTimeOffset Now = new(2026, 3, 12, 10, 30, 0, TimeSpan.Zero);

    public static readonly DateTime ModifiedSince = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static SyncTask Task(
        string taskId = ValidTaskId,
        string taskType = SyncTaskTypes.GetCustomers,
        DateTime? modifiedSince = null,
        bool withParameters = true)
        => new(
            taskId,
            taskType,
            withParameters ? new SyncParameters(modifiedSince ?? ModifiedSince) : null,
            Now.UtcDateTime);
}
