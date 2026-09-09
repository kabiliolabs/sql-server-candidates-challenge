using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Platform;
using Xunit;

namespace SyncAgent.Core.Tests
{
    public sealed class SyncPlatformClientTests
    {
        private const string TaskBody = @"{
            ""taskId"": ""01JQFG8N3XRTV5KHW2YP4M7B6C"",
            ""taskType"": ""GetCustomers"",
            ""parameters"": { ""modifiedSince"": ""2025-01-01T00:00:00Z"" },
            ""createdAt"": ""2026-03-12T10:30:00Z""
        }";

        private static SyncPlatformClient CreateClient(FakeHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5100/"),
            };

            return new SyncPlatformClient(
                httpClient,
                TestData.Options(),
                NullLogger<SyncPlatformClient>.Instance);
        }

        [Fact]
        public async Task GetNextTaskAsync_returns_null_when_the_queue_is_empty()
        {
            // 204 with an empty body is "no work", not an error. Treating it as a failure
            // would make an idle agent log an error every five seconds forever.
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent));

            var task = await client.GetNextTaskAsync(CancellationToken.None);

            task.Should().BeNull();
        }

        [Fact]
        public async Task GetNextTaskAsync_parses_a_queued_task()
        {
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, TaskBody));

            var task = await client.GetNextTaskAsync(CancellationToken.None);

            task.Should().NotBeNull();
            task!.TaskId.Should().Be("01JQFG8N3XRTV5KHW2YP4M7B6C");
            task.TaskType.Should().Be("GetCustomers");
            task.Parameters["modifiedSince"].Should().Be("2025-01-01T00:00:00Z");
            task.CreatedAt.Should().Be(new DateTimeOffset(2026, 3, 12, 10, 30, 0, TimeSpan.Zero));
        }

        [Fact]
        public async Task GetNextTaskAsync_reads_parameter_names_case_insensitively()
        {
            var body = TaskBody.Replace("modifiedSince", "ModifiedSince");
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body));

            var task = await client.GetNextTaskAsync(CancellationToken.None);

            task!.Parameters.ContainsKey("modifiedSince").Should().BeTrue();
        }

        [Fact]
        public async Task GetNextTaskAsync_treats_a_401_as_permanent()
        {
            // Retrying a rejected API key only delays the failure and loads a platform that
            // has already said no.
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.Unauthorized));

            var exception = await Assert.ThrowsAsync<PlatformApiException>(
                () => client.GetNextTaskAsync(CancellationToken.None));

            exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            exception.IsTransient.Should().BeFalse();
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.RequestTimeout)]
        public async Task GetNextTaskAsync_treats_server_side_failures_as_transient(HttpStatusCode status)
        {
            var client = CreateClient(FakeHttpMessageHandler.Returning(status));

            var exception = await Assert.ThrowsAsync<PlatformApiException>(
                () => client.GetNextTaskAsync(CancellationToken.None));

            exception.IsTransient.Should().BeTrue();
        }

        [Fact]
        public async Task GetNextTaskAsync_rejects_a_body_larger_than_the_limit()
        {
            // A task envelope is a few hundred bytes. Over plain HTTP on loopback the agent
            // cannot authenticate the server, so an unbounded read is an easy way to kill it.
            var oversized = new string('x', 2 * 1024 * 1024);
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, $@"{{""taskId"":""{oversized}""}}"));

            await Assert.ThrowsAsync<PlatformApiException>(
                () => client.GetNextTaskAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetNextTaskAsync_rejects_a_body_that_is_not_valid_JSON()
        {
            var client = CreateClient(FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "not json at all"));

            await Assert.ThrowsAsync<PlatformApiException>(
                () => client.GetNextTaskAsync(CancellationToken.None));
        }

        [Fact]
        public async Task SubmitResultAsync_posts_a_camelCase_body_to_the_result_endpoint()
        {
            // The single most important assertion about the wire format: the platform binds
            // with case sensitivity on, so PascalCase would produce "400 Missing required
            // field: taskId" for a body that plainly contains one.
            var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, @"{""accepted"":true}");
            var client = CreateClient(handler);
            var result = SyncTaskResult.Completed(TestData.Task(), new object[] { 1, 2 }, TestData.Now);

            await client.SubmitResultAsync(result, CancellationToken.None);

            handler.Requests.Should().ContainSingle();
            handler.Requests[0].Method.Should().Be(HttpMethod.Post);
            handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/api/sync/result");

            var body = handler.RequestBodies[0];
            body.Should().Contain("\"taskId\":");
            body.Should().Contain("\"recordCount\":2");
            body.Should().Contain("\"status\":\"completed\"");
            body.Should().NotContain("\"TaskId\":");
        }

        [Fact]
        public async Task SubmitResultAsync_refuses_a_payload_over_the_configured_size()
        {
            // Checked before sending: the platform enforces no body limit of its own, so an
            // oversized result would be accepted and become someone else's memory problem.
            var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, @"{""accepted"":true}");
            var options = TestData.Options();
            options.MaxResultPayloadBytes = 128;

            var client = new SyncPlatformClient(
                new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100/") },
                options,
                NullLogger<SyncPlatformClient>.Instance);

            var result = SyncTaskResult.Completed(
                TestData.Task(),
                new object[] { new string('x', 500) },
                TestData.Now);

            await Assert.ThrowsAsync<PlatformApiException>(
                () => client.SubmitResultAsync(result, CancellationToken.None));

            handler.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task SubmitResultAsync_surfaces_a_rejected_body()
        {
            var handler = FakeHttpMessageHandler.Returning(
                HttpStatusCode.BadRequest,
                @"{""accepted"":false,""error"":""Missing required field: taskId""}");
            var client = CreateClient(handler);

            var exception = await Assert.ThrowsAsync<PlatformApiException>(
                () => client.SubmitResultAsync(
                    SyncTaskResult.Completed(TestData.Task(), new object[0], TestData.Now),
                    CancellationToken.None));

            exception.IsTransient.Should().BeFalse();
        }

        [Fact]
        public void The_http_client_factory_sets_the_api_key_header_and_a_trailing_slash_base_address()
        {
            var options = TestData.Options();
            options.PlatformBaseUrl = new Uri("http://localhost:5100");

            using (var client = PlatformHttpClientFactory.Create(options))
            {
                client.DefaultRequestHeaders.GetValues(PlatformHttpClientFactory.ApiKeyHeaderName)
                    .Should().ContainSingle().Which.Should().Be(options.ApiKey);

                // Without the trailing slash, resolving a relative path replaces the last
                // segment instead of appending to it.
                client.BaseAddress!.AbsoluteUri.Should().EndWith("/");
                new Uri(client.BaseAddress, SyncPlatformClient.NextTaskPath).AbsolutePath
                    .Should().Be("/api/sync/next-task");
            }
        }
    }
}
