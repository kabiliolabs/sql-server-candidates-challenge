using System.Net;

namespace TaskExecutionService.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>: returns a preset response
/// and records the request (and its body) so tests can assert on what was sent.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public StubHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody = null)
    {
        _response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
        {
            _response.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return _response;
    }
}
