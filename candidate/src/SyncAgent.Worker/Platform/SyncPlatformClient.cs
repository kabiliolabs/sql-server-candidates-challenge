using System.Net;
using System.Net.Http.Json;

namespace SyncAgent.Worker.Platform;

public class SyncPlatformClient(HttpClient httpClient) : ISyncPlatformClient
{
    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "SyncPlatform rejected the request with 401 Unauthorized. Check the " +
                "SyncPlatform:ApiKey configuration value against the platform's expected key.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncTask>(cancellationToken);
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/sync/result", result, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "SyncPlatform rejected the request with 401 Unauthorized. Check the " +
                "SyncPlatform:ApiKey configuration value against the platform's expected key.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SyncPlatform rejected the result as invalid (400): {errorBody}");
        }

        response.EnsureSuccessStatusCode();
    }
}
