using Microsoft.Extensions.Logging;
using SyncAgent.Application.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SyncAgent.Infrastructure.Http;

public sealed class SyncPlatformHttpClient : ISyncPlatformClient
{
    // Reused rather than built per call: System.Text.Json caches its metadata per options instance.
    // Nulls are written rather than skipped because the contract's result payload carries
    // "data": null on failures and "errorMessage": null on successes as explicit fields.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<SyncPlatformHttpClient> _logger;

    // BaseAddress, Timeout and the X-Api-Key header are configured centrally in
    // ServiceCollectionExtensions. They must not also be set here: DefaultRequestHeaders.Add()
    // appends rather than replaces, so a second X-Api-Key would be sent comma-joined and fail
    // the platform's exact-match check on the key.
    public SyncPlatformHttpClient(HttpClient httpClient, ILogger<SyncPlatformHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NextTaskResponse?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        // Transport failures propagate to SyncTaskPoller, which owns how they are reported.
        var response = await _httpClient.GetAsync("/api/sync/next-task", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<NextTaskResponse>(content, SerializerOptions);
    }

    public async Task<ResultSubmissionResponse> SubmitResultAsync(
        string taskId,
        string taskType,
        string status,
        object? data,
        int recordCount,
        DateTime executedAt,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            taskId,
            taskType,
            status,
            data,
            recordCount,
            executedAt,
            errorMessage
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/sync/result", content, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Platform rejected the result for task {TaskId}: {StatusCode} {Content}",
                taskId, response.StatusCode, responseContent);
        }

        return JsonSerializer.Deserialize<ResultSubmissionResponse>(responseContent, SerializerOptions)
               ?? new ResultSubmissionResponse(false, taskId, "Failed to parse response");
    }
}
