using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SyncAgent.Infrastructure.Data.Queries;

namespace SyncAgent.Integration.Tests
{
    /// <summary>
    /// Runs the four sync queries against a real AdventureWorks database.
    /// </summary>
    /// <remarks>
    /// These verify the half of the work that unit tests structurally cannot: that the column
    /// names exist, that the joins produce the cardinality the contract expects, and that the
    /// values land in the right properties. A mocked repository proves the handler calls it —
    /// it proves nothing about whether the SQL is right.
    ///
    /// The assertions use fixed identifiers taken from the sample payloads, so they check the
    /// mapping against the documented contract rather than against whatever the query happens
    /// to return today.
    /// </remarks>
    public sealed class AdventureWorksQueryTests
    {
        /// <summary>The cutoff the platform actually sends.</summary>
        private static readonly DateTimeOffset ContractCutoff =
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>Orders were last modified in 2022-2025, so they need an earlier cutoff.</summary>
        private static readonly DateTimeOffset OrdersCutoff =
            new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset FutureCutoff =
            new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

        [RequiresDatabaseFact]
        public async Task Customers_map_exactly_to_the_sample_payload()
        {
            var query = new CustomerQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var customers = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            var jon = customers.Single(c => c.CustomerId == 11000);

            jon.AccountNumber.Should().Be("AW00011000");
            jon.FirstName.Should().Be("Jon");
            jon.LastName.Should().Be("Yang");
            jon.EmailAddress.Should().Be("jon24@adventure-works.com");
            jon.Phone.Should().Be("1 (11) 500 555-0162");

            // The address comes from an OUTER APPLY that orders by address type, so this also
            // pins the tie-break: a person with several addresses must always yield the same
            // one, or consecutive syncs disagree.
            jon.AddressLine1.Should().Be("3761 N. 14th St");
            jon.City.Should().Be("Rockhampton");
            jon.StateProvince.Should().Be("Queensland");
            jon.PostalCode.Should().Be("4700");
            jon.CountryRegion.Should().Be("Australia");
        }

        [RequiresDatabaseFact]
        public async Task Customers_without_an_associated_person_are_excluded()
        {
            // firstName and lastName are not optional in the contract, and 701 rows in
            // Sales.Customer are store accounts with a null PersonID. This pins that decision.
            var query = new CustomerQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var customers = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            customers.Should().NotBeEmpty();
            customers.Should().OnlyContain(c =>
                !string.IsNullOrWhiteSpace(c.FirstName) && !string.IsNullOrWhiteSpace(c.LastName));
        }

        [RequiresDatabaseFact]
        public async Task Products_map_exactly_to_the_sample_payload()
        {
            var query = new ProductQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var products = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            var frame = products.Single(p => p.ProductId == 680);

            frame.Name.Should().Be("HL Road Frame - Black, 58");
            frame.ProductNumber.Should().Be("FR-R92B-58");
            frame.Color.Should().Be("Black");
            frame.StandardCost.Should().Be(1059.31m);
            frame.ListPrice.Should().Be(1431.50m);
            frame.Category.Should().Be("Components");
            frame.Subcategory.Should().Be("Road Frames");
            frame.ModifiedDate.Should().Be(new DateTime(2025, 2, 7, 10, 1, 36, 827, DateTimeKind.Utc));
            frame.ModifiedDate.Kind.Should().Be(DateTimeKind.Utc);
        }

        [RequiresDatabaseFact]
        public async Task Products_without_a_subcategory_are_still_returned()
        {
            // The joins to subcategory and category are LEFT because 209 of 504 products have
            // none. An inner join would silently drop 41% of the catalogue.
            var query = new ProductQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var products = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            products.Should().Contain(p => p.Category == null);
            products.Should().HaveCount(504);
        }

        [RequiresDatabaseFact]
        public async Task Inventory_maps_exactly_to_the_sample_payload()
        {
            var query = new ProductInventoryQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var inventory = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            var race = inventory.Single(i => i.ProductId == 1 && i.LocationName == "Tool Crib");

            race.ProductName.Should().Be("Adjustable Race");
            race.ProductNumber.Should().Be("AR-5381");
            race.Shelf.Should().Be("A");
            race.Bin.Should().Be(1);
            race.Quantity.Should().Be(408);
            race.ModifiedDate.Should().Be(new DateTime(2025, 8, 7, 0, 0, 0, DateTimeKind.Utc));
        }

        [RequiresDatabaseFact]
        public async Task Inventory_is_grained_per_product_and_location()
        {
            // The same productId legitimately appears more than once. A reviewer seeing that
            // in the payload should be able to find the test that says it is intentional.
            var query = new ProductInventoryQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var inventory = await query.GetModifiedSinceAsync(ContractCutoff, 50_000, CancellationToken.None);

            inventory.Where(i => i.ProductId == 1).Should().HaveCountGreaterThan(1);
            inventory.Select(i => (i.ProductId, i.LocationName)).Should().OnlyHaveUniqueItems();
        }

        [RequiresDatabaseFact]
        public async Task Orders_map_exactly_to_the_sample_payload_including_nested_lines()
        {
            var query = new OrderQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var orders = await query.GetModifiedSinceAsync(OrdersCutoff, 50_000, CancellationToken.None);

            var order = orders.Single(o => o.SalesOrderId == 43659);

            order.OrderDate.Should().Be(new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc));
            order.Status.Should().Be(5);
            order.TotalDue.Should().Be(23153.2339m);

            // From Sales.Customer, not SalesOrderHeader — the header's own AccountNumber for
            // this order is "10-4020-000676".
            order.AccountNumber.Should().Be("AW00029825");

            // Customer 29825 is a store account that does have an associated person, and the
            // sample uses the person's name rather than the store's.
            order.CustomerName.Should().Be("James Hendergart");

            order.OrderDetails.Should().NotBeEmpty();

            var line = order.OrderDetails.First();
            line.ProductName.Should().Be("Mountain-100 Black, 42");
            line.ProductNumber.Should().Be("BK-M82B-42");
            line.UnitPrice.Should().Be(2024.9940m);
            line.Quantity.Should().Be(1);
        }

        [RequiresDatabaseFact]
        public async Task Every_returned_order_carries_its_own_lines()
        {
            // The two statements repeat the same TOP/ORDER BY in a CTE precisely so the
            // detail query cannot return lines for orders the header query did not.
            var query = new OrderQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var orders = await query.GetModifiedSinceAsync(OrdersCutoff, 25, CancellationToken.None);

            orders.Should().HaveCount(25);
            orders.Should().OnlyContain(o => o.OrderDetails.Count > 0);
            orders.Select(o => o.SalesOrderId).Should().BeInAscendingOrder();
        }

        [RequiresDatabaseFact]
        public async Task The_row_limit_is_applied_inside_the_query()
        {
            var query = new CustomerQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var customers = await query.GetModifiedSinceAsync(ContractCutoff, 10, CancellationToken.None);

            customers.Should().HaveCount(10);
            customers.Select(c => c.CustomerId).Should().BeInAscendingOrder();
        }

        [RequiresDatabaseFact]
        public async Task A_future_cutoff_returns_nothing_from_every_query()
        {
            // Proves the predicate is actually wired up. Without this, a query that ignored
            // modifiedSince entirely would pass every other test in this file.
            var factory = IntegrationTestEnvironment.ConnectionFactory();
            var options = IntegrationTestEnvironment.Options();

            (await new CustomerQuery(factory, options)
                .GetModifiedSinceAsync(FutureCutoff, 100, CancellationToken.None)).Should().BeEmpty();
            (await new ProductQuery(factory, options)
                .GetModifiedSinceAsync(FutureCutoff, 100, CancellationToken.None)).Should().BeEmpty();
            (await new OrderQuery(factory, options)
                .GetModifiedSinceAsync(FutureCutoff, 100, CancellationToken.None)).Should().BeEmpty();
            (await new ProductInventoryQuery(factory, options)
                .GetModifiedSinceAsync(FutureCutoff, 100, CancellationToken.None)).Should().BeEmpty();
        }

        [RequiresDatabaseFact]
        public async Task Results_are_stable_across_runs()
        {
            // Every query has a deterministic ORDER BY and every TOP 1 sub-select has a
            // tie-break, so two consecutive syncs of unchanged data must agree exactly.
            var query = new CustomerQuery(
                IntegrationTestEnvironment.ConnectionFactory(),
                IntegrationTestEnvironment.Options());

            var first = await query.GetModifiedSinceAsync(ContractCutoff, 200, CancellationToken.None);
            var second = await query.GetModifiedSinceAsync(ContractCutoff, 200, CancellationToken.None);

            second.Should().BeEquivalentTo(first, o => o.WithStrictOrdering());
        }
    }
}
