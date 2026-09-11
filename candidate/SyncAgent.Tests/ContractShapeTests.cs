using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SyncAgent.Contracts;

namespace SyncAgent.Tests
{
    /// <summary>Golden-shape checks against docs/sample-payloads result-*.json:
    /// property names, casing and nesting must match the platform contract.</summary>
    [TestFixture]
    public class ContractShapeTests
    {
        private static readonly string SamplesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");

        private static void AssertShapeMatches(string sampleFile, object actualRow)
        {
            string golden = File.ReadAllText(Path.Combine(SamplesDir, sampleFile));
            JObject envelope = JObject.Parse(golden);
            JToken expected = envelope["data"][0];
            JToken actual = JToken.FromObject(actualRow);
            AssertShapesEqual(expected, actual, "$");
        }

        private static void AssertShapesEqual(JToken expected, JToken actual, string path)
        {
            Assert.AreEqual(expected.Type, actual.Type, "token type at " + path);

            if (expected.Type == JTokenType.Object)
            {
                foreach (JProperty prop in ((JObject)expected).Properties())
                {
                    JToken actualProp = actual[prop.Name];
                    Assert.IsNotNull(actualProp, "missing property '" + prop.Name + "' at " + path);
                    AssertShapesEqual(prop.Value, actualProp, path + "." + prop.Name);
                }
            }
            else if (expected.Type == JTokenType.Array && expected.HasValues)
            {
                AssertShapesEqual(expected.First, actual.First, path + "[0]");
            }
        }

        [Test]
        public void CustomerRow_MatchesGoldenShape()
        {
            AssertShapeMatches("result-get-customers.json", new CustomerRow
            {
                customerId = 11000,
                accountNumber = "AW00011000",
                firstName = "Jon",
                lastName = "Yang",
                emailAddress = "jon24@adventure-works.com",
                phone = "1 (11) 500 555-0162",
                addressLine1 = "3761 N. 14th St",
                city = "Rockhampton",
                stateProvince = "Queensland",
                postalCode = "4700",
                countryRegion = "Australia"
            });
        }

        [Test]
        public void ProductRow_MatchesGoldenShape()
        {
            AssertShapeMatches("result-get-products.json", new ProductRow
            {
                productId = 1,
                name = "Adjustable Race",
                productNumber = "AR-5381",
                color = "Black",
                standardCost = 0m,
                listPrice = 0m,
                category = "Components",
                subcategory = "Bottom Brackets",
                modifiedDate = new DateTime(2025, 1, 1)
            });
        }

        [Test]
        public void OrderRow_MatchesGoldenShape_IncludingNestedDetails()
        {
            AssertShapeMatches("result-get-orders.json", new OrderRow
            {
                salesOrderId = 43659,
                orderDate = new DateTime(2022, 5, 30),
                status = 5,
                customerName = "James Hendergart",
                accountNumber = "AW00029825",
                totalDue = 23153.2339m,
                orderDetails = new List<OrderDetailRow>
                {
                    new OrderDetailRow
                    {
                        productName = "Mountain-100 Black, 42",
                        productNumber = "BK-M82B-42",
                        unitPrice = 2024.994m,
                        quantity = 1,
                        lineTotal = 2024.994m
                    }
                }
            });
        }

        [Test]
        public void InventoryRow_MatchesGoldenShape()
        {
            AssertShapeMatches("result-get-product-inventory.json", new InventoryRow
            {
                productId = 1,
                productName = "Adjustable Race",
                productNumber = "AR-5381",
                locationName = "Tool Crib",
                shelf = "A",
                bin = 1,
                quantity = 12,
                modifiedDate = new DateTime(2025, 1, 1)
            });
        }
    }
}