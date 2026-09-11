using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NUnit.Framework;
using SyncAgent.Domain;
using SyncAgent.Protocol;

namespace SyncAgent.Tests
{
    [TestFixture]
    public class PlatformClientTests
    {
        private static SyncPlatformClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            return new SyncPlatformClient(
                new HttpClient(new StubHttpMessageHandler(respond))
                {
                    BaseAddress = new Uri("http://localhost")
                },
                NullLogger<SyncPlatformClient>.Instance);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, object body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            };
        }

        [Test]
        public async Task GetNextTask_200_ParsesTask()
        {
            var client = Client(_ => Json(HttpStatusCode.OK, new
            {
                taskId = "01JQFGBK2FCST9QA4VXJR7D5NW",
                taskType = "GetOrders",
                parameters = new { modifiedSince = "2025-01-01T00:00:00Z" },
                createdAt = "2026-03-12T10:30:05Z"
            }));

            SyncTask task = await client.GetNextTaskAsync(CancellationToken.None);

            Assert.IsNotNull(task);
            Assert.AreEqual("GetOrders", task.taskType);
            Assert.AreEqual(
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                task.parameters.modifiedSince.Value);
        }

        [Test]
        public async Task GetNextTask_204_ReturnsNull_NotAnError()
        {
            var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

            Assert.IsNull(await client.GetNextTaskAsync(CancellationToken.None));
        }

        [Test]
        public void GetNextTask_401_Throws()
        {
            var client = Client(_ => Json(HttpStatusCode.Unauthorized, new { error = "Unauthorized. Provide a valid X-Api-Key header." }));

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.GetNextTaskAsync(CancellationToken.None));
        }

        [Test]
        public async Task PostResult_200Accepted_ReturnsTrue()
        {
            var client = Client(_ => Json(HttpStatusCode.OK, new { accepted = true, taskId = "t1" }));

            Assert.IsTrue(await client.PostResultAsync(
                SyncResult.Completed("t1", "GetProducts", new System.Collections.Generic.List<object>(), DateTime.UtcNow),
                CancellationToken.None));
        }

        [Test]
        public async Task PostResult_400_ReturnsFalse_NotThrown()
        {
            var client = Client(_ => Json(HttpStatusCode.BadRequest, new { accepted = false, error = "Status must be 'completed' or 'failed'" }));

            Assert.IsFalse(await client.PostResultAsync(
                SyncResult.Completed("t1", "GetProducts", new System.Collections.Generic.List<object>(), DateTime.UtcNow),
                CancellationToken.None));
        }

        [TestCase("completed")]
        [TestCase("failed")]
        public async Task PostResult_SendsCamelCasePayloadWithStatus(string status)
        {
            string captured = null;
            var client = Client(req =>
            {
                captured = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(HttpStatusCode.OK, new { accepted = true });
            });

            SyncResult result = status == "completed"
                ? SyncResult.Completed("t1", "GetProducts", new List<object>(), DateTime.UtcNow)
                : SyncResult.Failed("t1", "GetProducts", "boom", DateTime.UtcNow);

            await client.PostResultAsync(result, CancellationToken.None);

            StringAssert.Contains("\"taskId\":\"t1\"", captured);
            StringAssert.Contains("\"taskType\":\"GetProducts\"", captured);
            StringAssert.Contains("\"status\":\"" + status + "\"", captured);
        }
    }
}