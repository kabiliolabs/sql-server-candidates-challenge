using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Platform
{
    public sealed class PlatformClient(HttpClient httpClient, ILogger<PlatformClient> logger) : IPlatformClient
    {
        private const string NextTaskPath = "api/sync/next-task";
        private const string ResultPath = "api/sync/result";

        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new UtcDateTimeConverter() }
        };

        public async Task<SyncTask?> GetNextTaskAsync(CancellationToken cancellationToken)
        {
            using var response = await httpClient.GetAsync(NextTaskPath, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SyncTask>(JsonOptions, cancellationToken)
                   ?? throw new JsonException("Platform returned an empty task payload.");
        }

        public async Task PostResultAsync(SyncResult result, CancellationToken cancellationToken)
        {
            using var response = await httpClient.PostAsJsonAsync(ResultPath, result, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Platform rejected result for task {TaskId}: {StatusCode} {Body}",
                    result.TaskId, (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}