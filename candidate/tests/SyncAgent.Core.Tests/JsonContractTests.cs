using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SyncAgent.Core.Models;
using SyncAgent.Core.Models.Records;
using SyncAgent.Infrastructure.Platform;
using SyncAgent.Infrastructure.Serialization;
using Xunit;

namespace SyncAgent.Core.Tests
{
    /// <summary>
    /// Pins the wire format against the payloads shipped in docs/sample-payloads.
    /// </summary>
    /// <remarks>
    /// These are the most valuable tests in the suite, because the failure they guard against
    /// is silent and misattributed. The platform deserialises the result body with case
    /// sensitivity on, so a body with default .NET property naming binds nothing at all: the
    /// deserialisation succeeds, every field stays empty, and the agent is told
    /// "400 Missing required field: taskId" about a body that visibly contains one. Nothing in
    /// the type system catches that; the serializer configuration is one line and lives three
    /// projects away from the records it governs.
    ///
    /// Comparing key sets rather than whole documents keeps these tests about the contract:
    /// they fail when a property is renamed, added or dropped, and stay quiet when a value
    /// changes.
    /// </remarks>
    public sealed class JsonContractTests
    {
        [Fact]
        public void A_completed_result_has_exactly_the_keys_the_contract_defines()
        {
            var result = SyncTaskResult.Completed(TestData.Task(), new object[] { 1 }, TestData.Now);

            var json = JObject.Parse(SyncJson.Serialize(ToWireShape(result)));

            json.Properties().Select(p => p.Name).Should().BeEquivalentTo(new[]
            {
                "taskId", "taskType", "status", "data", "recordCount", "executedAt", "errorMessage",
            });
        }

        [Fact]
        public void The_status_values_are_the_exact_lowercase_literals_the_platform_compares_against()
        {
            // The platform tests these with ordinal, case-sensitive equality. "Completed"
            // would be rejected with 400.
            var completed = JObject.Parse(SyncJson.Serialize(
                ToWireShape(SyncTaskResult.Completed(TestData.Task(), new object[0], TestData.Now))));
            var failed = JObject.Parse(SyncJson.Serialize(
                ToWireShape(SyncTaskResult.Failed(TestData.Task(), "nope", TestData.Now))));

            completed.Value<string>("status").Should().Be("completed");
            failed.Value<string>("status").Should().Be("failed");
        }

        [Fact]
        public void Timestamps_are_serialised_with_a_trailing_Z()
        {
            // DateTimeOffset would render "+00:00" here. Every sample in the contract ends
            // in Z, so the record types use DateTime tagged Utc instead.
            var result = SyncTaskResult.Completed(TestData.Task(), new object[0], TestData.Now);

            var json = ParseWithoutDateConversion(SyncJson.Serialize(ToWireShape(result)));

            json.Value<string>("executedAt").Should().Be("2026-03-12T10:30:00Z");
        }

        [Fact]
        public void A_record_timestamp_is_serialised_with_a_trailing_Z()
        {
            var record = new ProductRecord
            {
                ModifiedDate = new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Utc),
            };

            var json = ParseWithoutDateConversion(SyncJson.Serialize(record));

            json.Value<string>("modifiedDate").Should().Be("2025-02-07T10:01:36.827Z");
        }

        [Fact]
        public void A_failed_result_carries_a_null_data_array_and_a_zero_record_count()
        {
            var result = SyncTaskResult.Failed(TestData.Task(), "Something went wrong.", TestData.Now);

            var json = JObject.Parse(SyncJson.Serialize(ToWireShape(result)));

            json["data"]!.Type.Should().Be(JTokenType.Null);
            json.Value<int>("recordCount").Should().Be(0);
            json.Value<string>("errorMessage").Should().Be("Something went wrong.");
        }

        [Fact]
        public void A_customer_record_matches_the_sample_payload_key_for_key()
        {
            var record = new CustomerRecord
            {
                CustomerId = 11000,
                AccountNumber = "AW00011000",
                FirstName = "Jon",
                LastName = "Yang",
                EmailAddress = "jon24@adventure-works.com",
                Phone = "1 (11) 500 555-0162",
                AddressLine1 = "3761 N. 14th St",
                City = "Rockhampton",
                StateProvince = "Queensland",
                PostalCode = "4700",
                CountryRegion = "Australia",
            };

            AssertMatchesSample("result-get-customers.json", record);
        }

        [Fact]
        public void A_product_record_matches_the_sample_payload_key_for_key()
        {
            var record = new ProductRecord
            {
                ProductId = 680,
                Name = "HL Road Frame - Black, 58",
                ProductNumber = "FR-R92B-58",
                Color = "Black",
                StandardCost = 1059.31m,
                ListPrice = 1431.50m,
                Category = "Components",
                Subcategory = "Road Frames",
                ModifiedDate = new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Utc),
            };

            AssertMatchesSample("result-get-products.json", record);
        }

        [Fact]
        public void An_inventory_record_matches_the_sample_payload_key_for_key()
        {
            var record = new ProductInventoryRecord
            {
                ProductId = 1,
                ProductName = "Adjustable Race",
                ProductNumber = "AR-5381",
                LocationName = "Tool Crib",
                Shelf = "A",
                Bin = 1,
                Quantity = 408,
                ModifiedDate = new DateTime(2025, 8, 7, 0, 0, 0, DateTimeKind.Utc),
            };

            AssertMatchesSample("result-get-product-inventory.json", record);
        }

        [Fact]
        public void An_order_record_and_its_nested_lines_match_the_sample_payload()
        {
            var record = new OrderRecord
            {
                SalesOrderId = 43659,
                OrderDate = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = 5,
                CustomerName = "James Hendergart",
                AccountNumber = "AW00029825",
                TotalDue = 23153.2339m,
                OrderDetails = new[]
                {
                    new OrderDetailRecord
                    {
                        ProductName = "Mountain-100 Black, 42",
                        ProductNumber = "BK-M82B-42",
                        UnitPrice = 2024.994m,
                        Quantity = 1,
                        LineTotal = 2024.994m,
                    },
                },
            };

            var actual = AssertMatchesSample("result-get-orders.json", record);

            var expectedLine = (JObject)LoadSample("result-get-orders.json")["data"]![0]!["orderDetails"]![0]!;
            var actualLine = (JObject)actual["orderDetails"]![0]!;

            actualLine.Properties().Select(p => p.Name)
                .Should().BeEquivalentTo(expectedLine.Properties().Select(p => p.Name));
        }

        [Fact]
        public void A_product_record_keeps_its_decimal_precision()
        {
            // Read back as decimal, not double: binary floating point cannot hold 1431.50
            // exactly, and a sync that shifts prices in the last cents is a defect that only
            // surfaces in someone's reconciliation months later.
            var record = new ProductRecord { ListPrice = 1431.50m, StandardCost = 1059.31m };

            var json = JObject.Parse(SyncJson.Serialize(record));

            json.Value<decimal>("listPrice").Should().Be(1431.50m);
            json.Value<decimal>("standardCost").Should().Be(1059.31m);
        }

        /// <summary>
        /// Serialises the record and asserts its key set equals that of the first element of
        /// the named sample's data array.
        /// </summary>
        private static JObject AssertMatchesSample(string sampleFileName, object record)
        {
            var expected = (JObject)LoadSample(sampleFileName)["data"]![0]!;
            var actual = JObject.Parse(SyncJson.Serialize(record));

            actual.Properties().Select(p => p.Name)
                .Should().BeEquivalentTo(expected.Properties().Select(p => p.Name));

            return actual;
        }

        /// <summary>
        /// Parses JSON while leaving timestamps as the strings they are on the wire.
        /// </summary>
        /// <remarks>
        /// <see cref="JObject.Parse(string)"/> recognises an ISO-8601 string and converts it
        /// to a <see cref="DateTime"/>, so reading it back as a string returns whatever the
        /// current culture renders — "03/12/2026 10:30:00" on this machine. That would make
        /// this test assert the test host's locale rather than the wire format.
        /// </remarks>
        private static JObject ParseWithoutDateConversion(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json))
                   {
                       DateParseHandling = DateParseHandling.None,
                   })
            {
                return JObject.Load(reader);
            }
        }

        private static JObject LoadSample(string fileName)
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(SamplePayloadDirectory.Value, fileName)));
        }

        /// <summary>
        /// Finds docs/sample-payloads by walking up from the test assembly.
        /// </summary>
        /// <remarks>
        /// A relative path with the right number of "../" segments works until someone changes
        /// the output path or the configuration, and then fails with a message about a file
        /// nobody recognises. Searching upwards survives both.
        /// </remarks>
        private static readonly Lazy<string> SamplePayloadDirectory = new Lazy<string>(() =>
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "docs", "sample-payloads");

                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate docs/sample-payloads above " + AppDomain.CurrentDomain.BaseDirectory);
        });

        /// <summary>
        /// The exact object the platform client serialises. Using the real DTO rather than a
        /// stand-in is the point: a hand-written mirror could drift away from it and keep
        /// passing.
        /// </summary>
        private static object ToWireShape(SyncTaskResult result) => SyncResultDto.FromDomain(result);
    }
}
