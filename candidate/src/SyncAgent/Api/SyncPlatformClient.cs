using SyncAgent.Models;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyncAgent.Api;

public sealed class SyncPlatformClient : ISyncPlatformClient
{
    private readonly HttpClient _httpClient;

    public SyncPlatformClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SyncTask?> GetNextTaskAsync(
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            "/api/sync/next-task",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var task = await response.Content.ReadFromJsonAsync<SyncTask>(
            cancellationToken: cancellationToken);

        return task
            ?? throw new InvalidOperationException(
                "The platform returned an empty task payload.");
    }

    public async Task SubmitResultAsync(
        SyncResult result,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/sync/result",
            result,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}