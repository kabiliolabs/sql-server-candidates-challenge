using System.Net;
using System.Text;

namespace SyncAgent.Tests.TestDoubles;

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

/// <summary>Replaces the network: returns canned responses and captures every request sent.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, headers, body));
        return respond(request);
    }

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string? json = null)
        => new(_ =>
        {
            var response = new HttpResponseMessage(statusCode);
            if (json is not null)
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return response;
        });
}
