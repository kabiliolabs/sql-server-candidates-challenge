using System.Net;

namespace SyncAgent.Tests.TestDoubles;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/>. Records every request it is asked to send --
/// including the request body, which <see cref="HttpClient"/> disposes before the caller sees it --
/// and answers from a queue of prepared responses.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<RecordedRequest> Requests { get; } = [];

    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string? json = null)
    {
        _responders.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = json is null
                ? new ByteArrayContent([])
                : new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

        return this;
    }

    public StubHttpMessageHandler RespondWith(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.TryGetValues("X-Api-Key", out var keys) ? [.. keys] : [],
            body));

        if (_responders.Count == 0)
        {
            throw new InvalidOperationException($"StubHttpMessageHandler received an unscripted {request.Method} {request.RequestUri}.");
        }

        return _responders.Dequeue()(request);
    }

    internal sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri, IReadOnlyList<string> ApiKeyHeaderValues, string? Body);
}
