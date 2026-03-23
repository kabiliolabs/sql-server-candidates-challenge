namespace SyncAgent.Infrastructure.Http;

/// <summary>
/// DelegatingHandler that attaches the X-Api-Key header to every outbound request.
/// </summary>
public class ApiKeyHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public ApiKeyHandler(string apiKey)
    {
        _apiKey = apiKey;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
        return base.SendAsync(request, ct);
    }
}
