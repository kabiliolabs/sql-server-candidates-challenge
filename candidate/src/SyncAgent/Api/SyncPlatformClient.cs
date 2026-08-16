using SyncAgent.Models;
using System.Net;
using System.Net.Http.Json;

namespace SyncAgent.Api;

public sealed class SyncPlatformClient : ISyncPlatformClient
{
    private readonly HttpClient _httpClient;

    public SyncPlatformClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(SyncPlatformClientsConstants.GetNextTask, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<SyncTask>(cancellationToken: cancellationToken);

        return task ?? throw new InvalidOperationException("The platform returned an empty task payload.");
    }

    public async Task SubmitResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(SyncPlatformClientsConstants.SubmitResult, result, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}