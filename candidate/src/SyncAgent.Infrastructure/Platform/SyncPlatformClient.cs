using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Serialization;

namespace SyncAgent.Infrastructure.Platform
{
    /// <inheritdoc cref="ISyncPlatformClient" />
    public sealed class SyncPlatformClient : ISyncPlatformClient
    {
        public const string NextTaskPath = "api/sync/next-task";
        public const string ResultPath = "api/sync/result";

        /// <summary>
        /// Ceiling on the body of a task response.
        /// </summary>
        /// <remarks>
        /// A task envelope is a few hundred bytes. Reading the body without a bound would let
        /// anything answering on that port stream until the agent runs out of memory, and on
        /// loopback plain HTTP that is not a hypothetical: the agent has no way to
        /// authenticate the server, only the other way round.
        /// </remarks>
        private const int MaxTaskResponseBytes = 1024 * 1024;

        private readonly HttpClient _httpClient;
        private readonly SyncAgentOptions _options;
        private readonly ILogger<SyncPlatformClient> _logger;

        public SyncPlatformClient(
            HttpClient httpClient,
            SyncAgentOptions options,
            ILogger<SyncPlatformClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SyncTaskRequest?> GetNextTaskAsync(CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, NextTaskPath))
            using (var response = await _httpClient
                       .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return null;
                }

                EnsureSuccess(response, NextTaskPath);

                var body = await ReadBoundedAsync(response.Content, MaxTaskResponseBytes).ConfigureAwait(false);

                SyncTaskDto? dto;
                try
                {
                    dto = SyncJson.Deserialize<SyncTaskDto>(body);
                }
                catch (JsonException ex)
                {
                    throw new PlatformApiException(
                        "The platform returned a task body that is not valid JSON.",
                        ex);
                }

                if (dto is null)
                {
                    throw new PlatformApiException("The platform returned an empty task body.");
                }

                return ToDomain(dto);
            }
        }

        public async Task SubmitResultAsync(SyncTaskResult result, CancellationToken cancellationToken)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var json = SyncJson.Serialize(SyncResultDto.FromDomain(result));
            var payloadBytes = Encoding.UTF8.GetByteCount(json);

            // Checked before sending rather than after a failure: the platform has no body
            // size limit of its own, so an oversized result would be accepted and quietly
            // become someone else's memory problem.
            if (payloadBytes > _options.MaxResultPayloadBytes)
            {
                throw new PlatformApiException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The serialised result is {0} bytes, over the configured limit of {1}.",
                    payloadBytes,
                    _options.MaxResultPayloadBytes));
            }

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, ResultPath) { Content = content })
            using (var response = await _httpClient
                       .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                EnsureSuccess(response, ResultPath);

                _logger.LogDebug(
                    "Submitted result for task {TaskId} ({PayloadBytes} bytes).",
                    result.TaskId,
                    payloadBytes);
            }
        }

        private static SyncTaskRequest ToDomain(SyncTaskDto dto)
        {
            // Ordinal-ignore-case so a platform that ships "ModifiedSince" one day does not
            // turn every task into a validation failure. The dictionary the platform sends is
            // plain JSON, with no casing guarantee beyond the current implementation.
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (dto.Parameters != null)
            {
                foreach (var pair in dto.Parameters)
                {
                    if (pair.Key != null)
                    {
                        parameters[pair.Key] = pair.Value;
                    }
                }
            }

            return new SyncTaskRequest(dto.TaskId, dto.TaskType, parameters, dto.CreatedAt);
        }

        private void EnsureSuccess(HttpResponseMessage response, string path)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var status = response.StatusCode;

            // Split once, here, where the status code is in hand. The retry policy then only
            // has to read IsTransient rather than re-deriving intent from a message.
            var isTransient =
                (int)status >= 500 ||
                status == HttpStatusCode.RequestTimeout ||
                status == (HttpStatusCode)429;

            if (status == HttpStatusCode.Unauthorized)
            {
                // Worth its own log line: this is almost always a stale or missing API key,
                // and it will not resolve itself no matter how long the agent keeps polling.
                _logger.LogError(
                    "The platform rejected the API key on {Path}. Check the configured credential.",
                    path);
            }

            throw new PlatformApiException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The platform returned {0} ({1}) for {2}.",
                    (int)status,
                    status,
                    path),
                status,
                isTransient);
        }

        /// <summary>
        /// Reads a response body, refusing anything over <paramref name="maxBytes"/>.
        /// </summary>
        /// <remarks>
        /// The declared Content-Length is checked first as a cheap rejection, then the read
        /// itself is capped, because Content-Length is a claim by the sender and a chunked
        /// response does not have to declare one at all.
        /// </remarks>
        private static async Task<string> ReadBoundedAsync(HttpContent content, int maxBytes)
        {
            if (content.Headers.ContentLength is long declaredLength && declaredLength > maxBytes)
            {
                throw new PlatformApiException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The platform response declares {0} bytes, over the {1} byte limit.",
                    declaredLength,
                    maxBytes));
            }

            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                int read;

                while ((read = await stream.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + read > maxBytes)
                    {
                        throw new PlatformApiException(string.Format(
                            CultureInfo.InvariantCulture,
                            "The platform response exceeded the {0} byte limit.",
                            maxBytes));
                    }

                    buffer.Write(chunk, 0, read);
                }

                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
        }
    }
}
