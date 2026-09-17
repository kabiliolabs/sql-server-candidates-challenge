using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Tasks;

namespace SyncAgent.Infrastructure.Platform;

public sealed class SyncPlatformClient(HttpClient httpClient) : ISyncPlatformClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var wire = await response.Content.ReadFromJsonAsync<SyncTaskWire>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Platform returned 200 for next-task with an empty body.");

        return new SyncTask
        {
            TaskId = wire.TaskId,
            TaskType = wire.TaskType,
            Parameters = wire.Parameters,
            CreatedAt = wire.CreatedAt
        };
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        var wire = new SyncResultWire(
            result.TaskId,
            result.TaskType,
            result.Status,
            result.Data,
            result.RecordCount,
            result.ExecutedAt,
            result.ErrorMessage);

        using var response = await httpClient.PostAsJsonAsync("api/sync/result", wire, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
