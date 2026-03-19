using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncAgent.Contracts;

namespace SyncAgent.Infrastructure.Http;

public sealed class SyncPlatformClient(HttpClient httpClient, ILogger<SyncPlatformClient> logger) : ISyncPlatformClient
{
    public const string ApiKeyHeaderName = "X-Api-Key";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SyncTaskContract?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            logger.LogDebug("No sync tasks available.");
            return null;
        }

        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<SyncTaskContract>(SerializerOptions, cancellationToken);
        return task;
    }

    public async Task SubmitResultAsync(SyncResultContract result, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/sync/result", result, SerializerOptions, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Result submission failed. StatusCode: {StatusCode}; Response: {ResponseBody}",
            response.StatusCode,
            responseBody);

        throw new HttpRequestException(
            $"Result submission failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
    }
}
