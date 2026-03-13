using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncAgent.Models;

namespace SyncAgent.Services;

public class PlatformApiClient : IPlatformApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlatformApiClient(HttpClient httpClient, ILogger<PlatformApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<SyncTask>(JsonOptions, cancellationToken);
        return task;
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/sync/result", result, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Platform rejected result for task {TaskId}: {StatusCode} — {Body}",
                result.TaskId, response.StatusCode, body);
        }

        response.EnsureSuccessStatusCode();
    }
}
