using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncAgent.Configuration;
using SyncAgent.Platform.Contracts;

namespace SyncAgent.Platform;

/// <summary>
/// Typed <see cref="HttpClient"/> over the platform's two endpoints.
/// </summary>
/// <remarks>
/// The base address and API key are applied here rather than in DI wiring, so the client is
/// correct by construction and can be exercised in tests against a stub message handler.
/// </remarks>
internal sealed class PlatformClient : IPlatformClient
{
    internal const string ApiKeyHeaderName = "X-Api-Key";

    private const string NextTaskPath = "api/sync/next-task";
    private const string ResultPath = "api/sync/result";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformClient> _logger;

    public PlatformClient(HttpClient httpClient, IOptions<PlatformOptions> options, ILogger<PlatformClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var platform = options.Value;

        // Pinning BaseAddress means every request is resolved relative to the configured origin.
        // The agent never follows a URL supplied by the platform.
        _httpClient.BaseAddress = EnsureTrailingSlash(platform.BaseAddress);
        _httpClient.Timeout = platform.HttpTimeout;
        _httpClient.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, platform.ApiKey);
    }

    public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(NextTaskPath, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            _logger.LogDebug("Platform reported an empty queue (204).");
            return null;
        }

        // Throws on 401/403/5xx. The message carries the status code, never the API key --
        // the key only ever lives in a request header, and we log the header's name, not its value.
        response.EnsureSuccessStatusCode();

        var task = await response.Content
            .ReadFromJsonAsync<SyncTask>(PlatformSerialization.Options, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            _logger.LogWarning("Platform returned {StatusCode} with a body that deserialised to null.", (int)response.StatusCode);
        }

        return task;
    }

    public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        using var response = await _httpClient
            .PostAsJsonAsync(ResultPath, result, PlatformSerialization.Options, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Posted {Status} result for task {TaskId} ({TaskType}) with {RecordCount} record(s).",
            result.Status,
            result.TaskId,
            result.TaskType,
            result.RecordCount);
    }

    /// <summary>
    /// <see cref="Uri"/> relative resolution drops the last path segment when the base address
    /// has no trailing slash, so normalise it once here.
    /// </summary>
    private static Uri EnsureTrailingSlash(Uri baseAddress) =>
        baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");
}
