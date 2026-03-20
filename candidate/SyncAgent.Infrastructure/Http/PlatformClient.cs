using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SyncAgent.Core.Interfaces;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Http;

/// <summary>
/// Communicates with the central sync platform API.
/// HttpClient is injected via IHttpClientFactory (registered as a typed client in Program.cs).
/// </summary>
public class PlatformClient : IPlatformClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PlatformClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncTask>(ReadOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/sync/result", result, WriteOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
