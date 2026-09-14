using System.Net;
using System.Net.Http.Json;
using SyncAgent.Contracts;

namespace SyncAgent.Api;

/// <summary>
/// Typed HttpClient implementation of <see cref="ISyncPlatformApiClient"/>.
/// The X-Api-Key header and base address/timeout are configured on the HttpClient
/// by the DI registration (see Program.cs), consistent with resilience policies
/// applied via Microsoft.Extensions.Http.Resilience.
/// </summary>
public sealed class SyncPlatformApiClient(HttpClient httpClient, ILogger<SyncPlatformApiClient> logger)
    : ISyncPlatformApiClient
{
    private const string NextTaskEndpoint = "api/sync/next-task";
    private const string ResultEndpoint = "api/sync/result";

    public async Task<SyncTaskDto?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(NextTaskEndpoint, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError("SyncPlatform rejected the API key (401) when requesting the next task.");
            response.EnsureSuccessStatusCode();
        }

        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<SyncTaskDto>(cancellationToken);
        if (task is not null && (string.IsNullOrWhiteSpace(task.TaskId) || string.IsNullOrWhiteSpace(task.TaskType)))
        {
            throw new InvalidOperationException("SyncPlatform returned a task with a missing taskId or taskType.");
        }

        return task;
    }

    public async Task PostResultAsync(SyncResultDto result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        using var response = await httpClient.PostAsJsonAsync(ResultEndpoint, result, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("SyncPlatform rejected the result payload for task {TaskId} (400): {Body}", result.TaskId, body);
        }

        response.EnsureSuccessStatusCode();
    }
}
