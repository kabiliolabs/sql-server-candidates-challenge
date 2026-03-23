using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Http;

public class PlatformClient : IPlatformClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PlatformClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlatformClient(HttpClient http, ILogger<PlatformClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/sync/next-task", ct);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncTask>(JsonOptions, ct);
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/sync/result", result, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to post result for {TaskId}: {Status} {Body}",
                result.TaskId, response.StatusCode, body);
        }

        response.EnsureSuccessStatusCode();
    }
}
