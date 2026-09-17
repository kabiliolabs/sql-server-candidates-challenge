using Microsoft.Extensions.Options;

namespace SyncAgent.Infrastructure.Platform;

/// <summary>Attaches X-Api-Key to every outgoing request. Kept as its own handler (rather than
/// inline in SyncPlatformClient) so it composes cleanly with the resilience pipeline added
/// later via AddResilienceHandler.</summary>
public sealed class ApiKeyDelegatingHandler(IOptions<PlatformOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Api-Key");
        request.Headers.Add("X-Api-Key", options.Value.ApiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
