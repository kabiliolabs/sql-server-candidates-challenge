using Microsoft.Extensions.Options;
using SyncAgent.Core.Configuration;

namespace SyncAgent.Infrastructure.Http;

/// <summary>
/// Automatically injects the X-Api-Key header into every outgoing HTTP request.
/// Using a DelegatingHandler keeps auth logic out of the client itself.
/// </summary>
public class ApiKeyDelegatingHandler : DelegatingHandler
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly string _apiKey;

    public ApiKeyDelegatingHandler(IOptions<SyncAgentOptions> options)
    {
        _apiKey = options.Value.ApiKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(ApiKeyHeader, _apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
