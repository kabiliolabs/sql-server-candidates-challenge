using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncAgent.Domain;

namespace SyncAgent.Protocol
{
    /// <summary>Typed client for the central platform. 204 means "no work", not an error;
    /// rejected results are logged so failures are observable.</summary>
    public sealed class SyncPlatformClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<SyncPlatformClient> _logger;

        public SyncPlatformClient(HttpClient http, ILogger<SyncPlatformClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<SyncTask> GetNextTaskAsync(CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await _http
                .GetAsync("api/sync/next-task", cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                    return null;

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Platform rejected API key (401) picking up next task.");
                    throw new InvalidOperationException(
                        "Unauthorized (401) from platform; check Platform:ApiKey.");
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<SyncTask>(json);
            }
        }

        public async Task<bool> PostResultAsync(SyncResult result, CancellationToken cancellationToken)
        {
            string json = JsonConvert.SerializeObject(result);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await _http
                .PostAsync("api/sync/result", content, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError(
                        "POST /api/sync/result for task {TaskId} rejected with HTTP {StatusCode}: {Body}",
                        result.taskId, (int)response.StatusCode, body);
                    return false;
                }

                string acceptedBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AcceptedResponse accepted = JsonConvert.DeserializeObject<AcceptedResponse>(acceptedBody);
                if (accepted == null || !accepted.accepted)
                {
                    _logger.LogWarning("Platform did not accept result for task {TaskId}.", result.taskId);
                    return false;
                }
                return true;
            }
        }

        private sealed class AcceptedResponse
        {
            public bool accepted { get; set; }
        }
    }
}