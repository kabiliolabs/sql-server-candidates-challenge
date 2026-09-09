using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using SyncAgent.Core.Configuration;

namespace SyncAgent.Infrastructure.Platform
{
    /// <summary>
    /// Builds the single <see cref="HttpClient"/> the agent uses for its whole lifetime.
    /// </summary>
    /// <remarks>
    /// .NET Framework 4.8 has no IHttpClientFactory in the box, so the two problems it exists
    /// to solve are handled explicitly here.
    ///
    /// Socket exhaustion: a new HttpClient per request leaves its connection in TIME_WAIT for
    /// four minutes on Windows. An agent polling every five seconds would accumulate them
    /// until the ephemeral port range runs out — a failure that looks like a network outage
    /// and only appears after hours of uptime. One long-lived client avoids it.
    ///
    /// Stale DNS: the other side of a long-lived client is that it pins a resolved address
    /// forever. ConnectionLeaseTimeout forces the connection to be recycled periodically so a
    /// platform that moves behind a changed DNS record is picked up without a restart.
    /// </remarks>
    public static class PlatformHttpClientFactory
    {
        public const string ApiKeyHeaderName = "X-Api-Key";

        private static readonly TimeSpan ConnectionLeaseTimeout = TimeSpan.FromMinutes(5);

        public static HttpClient Create(SyncAgentOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.PlatformBaseUrl is null)
            {
                throw new SyncAgentConfigurationException("Platform base URL is missing.");
            }

            // .NET Framework 4.8 defaults SecurityProtocol to the OS setting, which on older
            // images still allows TLS 1.0. Pinning 1.2 and 1.3 costs nothing here — the test
            // platform is plain HTTP on loopback — and is required the moment this agent
            // points at a real https endpoint.
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | (SecurityProtocolType)12288 /* Tls13 */;

            var client = new HttpClient
            {
                BaseAddress = NormalizeBaseAddress(options.PlatformBaseUrl),
                Timeout = options.HttpTimeout,
            };

            try
            {
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                // The key is set once on the shared client rather than per request, so no
                // call site can forget it and no code path can log a request that carries it.
                client.DefaultRequestHeaders.Add(ApiKeyHeaderName, options.ApiKey);

                ServicePointManager
                    .FindServicePoint(client.BaseAddress)
                    .ConnectionLeaseTimeout = (int)ConnectionLeaseTimeout.TotalMilliseconds;

                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Guarantees the base address ends in a slash.
        /// </summary>
        /// <remarks>
        /// Without the trailing slash, <c>new Uri(baseAddress, "api/sync/next-task")</c>
        /// replaces the last path segment instead of appending to it. With a base of
        /// "http://localhost:5100" the two happen to agree, which is exactly why this bug
        /// survives until someone deploys behind a path prefix.
        /// </remarks>
        private static Uri NormalizeBaseAddress(Uri baseAddress)
        {
            var text = baseAddress.AbsoluteUri;

            return text.EndsWith("/", StringComparison.Ordinal)
                ? baseAddress
                : new Uri(text + "/", UriKind.Absolute);
        }
    }
}
