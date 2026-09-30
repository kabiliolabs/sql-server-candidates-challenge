using System.Net;
using Microsoft.Extensions.Options;
using Shouldly;
using SyncAgent.Infrastructure.Options;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Tests.TestDoubles;

namespace SyncAgent.Tests.Platform;

public sealed class ApiKeyHandlerTests
{
    private const string ApiKey = "candidate-test-key-2026";

    private static (HttpClient Client, StubHttpMessageHandler Stub) CreateClient()
    {
        var stub = StubHttpMessageHandler.Returning(HttpStatusCode.NoContent);
        var options = Microsoft.Extensions.Options.Options.Create(new PlatformOptions
        {
            BaseUrl = "http://localhost:5100",
            ApiKey = ApiKey
        });
        var handler = new ApiKeyHandler(options) { InnerHandler = stub };
        return (new HttpClient(handler), stub);
    }

    [Fact]
    public async Task Api_key_header_is_added_to_every_request()
    {
        var (client, stub) = CreateClient();

        await client.GetAsync("http://localhost:5100/api/sync/next-task");
        await client.PostAsync("http://localhost:5100/api/sync/result", new StringContent("{}"));

        stub.Requests.Count.ShouldBe(2);
        stub.Requests.ShouldAllBe(r => r.Headers[ApiKeyHandler.HeaderName] == ApiKey);
    }

    [Fact]
    public async Task Existing_api_key_header_is_replaced_not_duplicated()
    {
        var (client, stub) = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5100/api/sync/next-task");
        request.Headers.Add(ApiKeyHandler.HeaderName, "some-other-key");

        await client.SendAsync(request);

        stub.Requests.Single().Headers[ApiKeyHandler.HeaderName].ShouldBe(ApiKey);
    }
}
