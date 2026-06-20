using SyncTaskWorkerService.Models;
using System.Net;
using System.Text.Json;

namespace SyncTaskWorkerService.CentralPlatformHttpClient
{
    public class PlatformHttpClient(HttpClient _httpClient) : IPlatformHttpClient
    {
        public async Task<SyncTask?> GetTaskAsync(CancellationToken cancellationToken)
        {
            var result = await _httpClient.GetAsync("api/sync/next-task", cancellationToken);

            if (result.StatusCode == HttpStatusCode.OK)
            {
                var content = await result.Content.ReadAsStringAsync(cancellationToken);
                var task = JsonSerializer.Deserialize<SyncTask>(content);
                return task;
            }
            else
            {
                return null;
            }
        }

        public async Task<string> PostResultAsync(SyncResult result, CancellationToken cancellationToken)
        {
            HttpContent content = new StringContent(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
                , System.Text.Encoding.UTF8, "application/json"
            );

            var response = await _httpClient.PostAsync("/api/sync/result", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return "Failed :" + response.StatusCode;
            } 
            return "Accepted";
        }
    }
}
