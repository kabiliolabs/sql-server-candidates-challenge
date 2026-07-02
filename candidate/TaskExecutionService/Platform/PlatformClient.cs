using System.Net;
using System.Net.Http.Json;

namespace TaskExecutionService.Platform;

/// <summary>
/// Typed HttpClient talking to the platform. The base address and the
/// <c>X-Api-Key</c> header are configured on the injected <see cref="HttpClient"/>
/// in <c>Program.cs</c>, keeping the secret out of this class.
/// </summary>
public sealed class PlatformClient : IPlatformClient
{
    private const string NextTaskPath = "/api/sync/next-task";
    private const string ResultPath = "/api/sync/result";

    private readonly HttpClient _http;
    private readonly ILogger<PlatformClient> _logger;

    public PlatformClient(HttpClient http, ILogger<PlatformClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(NextTaskPath, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SyncTask>(SyncJson.Options, cancellationToken);
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(result, options: SyncJson.Options);
        using var response = await _http.PostAsync(ResultPath, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Platform rejected result for task {TaskId} ({TaskType}): {StatusCode} {Body}",
                result.TaskId, result.TaskType, (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }
}
