using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Handlers;
using SyncAgent.Sql;

namespace SyncAgent.Tests.Integration
{
    /// <summary>Runs against the real AdventureWorks2025 LocalDB instance.
    /// Requires ADVENTUREWORKS_CONN env var; skipped otherwise
    /// (dotnet test --filter Category!=Integration runs the fast suite only).</summary>
    [TestFixture]
    [Category("Integration")]
    public class HandlerIntegrationTests
    {
        private static readonly string ConnString = Environment.GetEnvironmentVariable("ADVENTUREWORKS_CONN");

        private static AgentOptions Options
        {
            get
            {
                return new AgentOptions
                {
                    ConnectionString = ConnString,
                    Url = "http://localhost:5100",
                    ApiKey = "test",
                    PollIntervalMs = 5000,
                    MaxRows = 10000,
                    CommandTimeoutSeconds = 30
                };
            }
        }

        private static SqlConnectionFactory Factory => new SqlConnectionFactory(ConnString);

        private static SyncTask TaskOf(string type)
        {
            return new SyncTask
            {
                taskId = "integration-" + type,
                taskType = type,
                parameters = null,
                createdAt = DateTime.UtcNow
            };
        }

        [Test]
        public async Task GetCustomers_ReturnsKnownRow()
        {
            if (string.IsNullOrEmpty(ConnString))
                Assert.Ignore("Set ADVENTUREWORKS_CONN to run integration tests.");

            var handler = new GetCustomersHandler(Factory, Options, NullLogger<GetCustomersHandler>.Instance);
            SyncResult result = await handler.ExecuteAsync(TaskOf("GetCustomers"), CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.Greater(result.recordCount, 0);
            CustomerRow jon = result.data.OfType<CustomerRow>()
                .FirstOrDefault(c => c.customerId == 11000);
            Assert.IsNotNull(jon, "customer 11000 must be present in AdventureWorks2025");
            Assert.AreEqual("Jon", jon.firstName);
            Assert.AreEqual("Yang", jon.lastName);
            Assert.AreEqual("Australia", jon.countryRegion);
        }

        [Test]
        public async Task GetProducts_ReturnsRows_WithCategoryJoin()
        {
            if (string.IsNullOrEmpty(ConnString))
                Assert.Ignore("Set ADVENTUREWORKS_CONN to run integration tests.");

            var handler = new GetProductsHandler(Factory, Options, NullLogger<GetProductsHandler>.Instance);
            SyncResult result = await handler.ExecuteAsync(TaskOf("GetProducts"), CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.Greater(result.recordCount, 0);
            var rows = result.data.OfType<ProductRow>().ToList();
            Assert.IsNotEmpty(rows.Where(p => !string.IsNullOrEmpty(p.name)));
            Assert.IsNotEmpty(rows.Where(p => !string.IsNullOrEmpty(p.productNumber)));
        }

        [Test]
        public async Task GetOrders_ReturnsNestedOrderDetails()
        {
            if (string.IsNullOrEmpty(ConnString))
                Assert.Ignore("Set ADVENTUREWORKS_CONN to run integration tests.");

            var handler = new GetOrdersHandler(Factory, Options, NullLogger<GetOrdersHandler>.Instance);
            SyncResult result = await handler.ExecuteAsync(TaskOf("GetOrders"), CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.Greater(result.recordCount, 0);
            var rows = result.data.OfType<OrderRow>().ToList();
            Assert.IsTrue(rows.Any(o => o.orderDetails != null && o.orderDetails.Count > 0),
                "at least one order must carry orderDetails");
        }

        [Test]
        public async Task GetProductInventory_ReturnsRows_WithLocation()
        {
            if (string.IsNullOrEmpty(ConnString))
                Assert.Ignore("Set ADVENTUREWORKS_CONN to run integration tests.");

            var handler = new GetProductInventoryHandler(Factory, Options, NullLogger<GetProductInventoryHandler>.Instance);
            SyncResult result = await handler.ExecuteAsync(TaskOf("GetProductInventory"), CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.Greater(result.recordCount, 0);
            var rows = result.data.OfType<InventoryRow>().ToList();
            Assert.IsNotEmpty(rows.Where(i => !string.IsNullOrEmpty(i.locationName)));
        }

        [Test]
        public async Task ModifiedSince_FiltersOutEverything()
        {
            if (string.IsNullOrEmpty(ConnString))
                Assert.Ignore("Set ADVENTUREWORKS_CONN to run integration tests.");

            var task = TaskOf("GetCustomers");
            task.parameters = new TaskParameters { modifiedSince = DateTime.UtcNow.AddDays(1) };

            var handler = new GetCustomersHandler(Factory, Options, NullLogger<GetCustomersHandler>.Instance);
            SyncResult result = await handler.ExecuteAsync(task, CancellationToken.None);

            Assert.AreEqual("completed", result.status);
            Assert.AreEqual(0, result.recordCount,
                "modifiedSince in the future must filter out every row (parameterized SQL honored)");
        }
    }
}