using System.Net;
using Moq;
using Moq.Protected;

namespace SyncAgent.Tests.Api;

/// <summary>
/// A stub HttpMessageHandler that returns a fixed HttpResponseMessage,
/// allowing unit tests to exercise HttpClient-based code without real network calls.
/// </summary>
internal static class FakeHttpMessageHandler
{
    public static HttpClient CreateClient(HttpResponseMessage response, Uri? baseAddress = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = baseAddress ?? new Uri("http://localhost:5100")
        };
        return client;
    }
}
