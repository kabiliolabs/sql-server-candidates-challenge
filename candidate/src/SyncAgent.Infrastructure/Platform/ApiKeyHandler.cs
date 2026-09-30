using Microsoft.Extensions.Options;

using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Platform
{
    public sealed class ApiKeyHandler(IOptions<PlatformOptions> options) : DelegatingHandler
    {
        public const string HeaderName = "X-Api-Key";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove(HeaderName);
            request.Headers.Add(HeaderName, options.Value.ApiKey);
            return base.SendAsync(request, cancellationToken);
        }
    }
}