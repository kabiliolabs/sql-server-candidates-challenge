namespace SyncAgent.Worker.Tests.Platform;

/// <summary>
/// Returns a canned HttpResponseMessage instead of making a real HTTP call, so
/// SyncPlatformClient's response-handling logic (200/204/400/401) can be tested without
/// a running SyncPlatform instance.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
