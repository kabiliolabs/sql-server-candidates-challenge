using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Infrastructure.Data.Queries
{
    /// <inheritdoc cref="IOrderQuery" />
    public sealed class OrderQuery : IOrderQuery
    {
        /*
            The only nested payload in the contract, and the only query that needs more than
            one result set.

            Shape: two SELECTs in one batch, read with NextResult(), assembled in memory.
            The alternative of a single flattened join would repeat every header column once
            per order line — 40,370 lines against 12,304 orders in this dataset — and then
            need the same grouping done client-side anyway. FOR JSON PATH would build the
            nested document server-side in one statement, but it would move the wire contract
            into the SQL text, where it cannot be unit tested and where a change to the
            payload becomes a change to a string literal.

            Both statements repeat the same TOP/ORDER BY inside a CTE. That is what guarantees
            the second SELECT sees exactly the orders the first one returned: with a row cap
            in play, filtering the lines by @modifiedSince alone would pull in lines belonging
            to orders past the cap.

            One batch rather than two round trips keeps the gap between the two reads as small
            as it can be without an explicit transaction. This is an eventually-consistent
            sync, so a line inserted between the two SELECTs simply arrives on the next run;
            a client database with READ_COMMITTED_SNAPSHOT enabled would close even that gap.

            Field mapping worth noting: accountNumber comes from Sales.Customer, not from
            SalesOrderHeader. Both columns exist and they are different things — the header's
            is '10-4020-000676', the customer's is 'AW00029825', and the sample shows the
            latter. customerName falls back to the store name for the 701 customers with no
            associated person, so a store order is not emitted with a null name.
        */
        private const string HeaderAndDetailSql = @"
WITH selected_orders AS (
    SELECT TOP (@maxRecords) soh.SalesOrderID
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.ModifiedDate >= @modifiedSince
    ORDER BY soh.SalesOrderID
)
SELECT
    h.SalesOrderID,
    h.OrderDate,
    h.Status,
    COALESCE(pe.FirstName + ' ' + pe.LastName, st.Name) AS CustomerName,
    cu.AccountNumber,
    h.TotalDue
FROM selected_orders AS so
INNER JOIN Sales.SalesOrderHeader AS h
    ON h.SalesOrderID = so.SalesOrderID
INNER JOIN Sales.Customer AS cu
    ON cu.CustomerID = h.CustomerID
LEFT JOIN Person.Person AS pe
    ON pe.BusinessEntityID = cu.PersonID
LEFT JOIN Sales.Store AS st
    ON st.BusinessEntityID = cu.StoreID
ORDER BY h.SalesOrderID;

WITH selected_orders AS (
    SELECT TOP (@maxRecords) soh.SalesOrderID
    FROM Sales.SalesOrderHeader AS soh
    WHERE soh.ModifiedDate >= @modifiedSince
    ORDER BY soh.SalesOrderID
)
SELECT
    d.SalesOrderID,
    pr.Name AS ProductName,
    pr.ProductNumber,
    d.UnitPrice,
    d.OrderQty,
    d.LineTotal
FROM selected_orders AS so
INNER JOIN Sales.SalesOrderDetail AS d
    ON d.SalesOrderID = so.SalesOrderID
INNER JOIN Production.Product AS pr
    ON pr.ProductID = d.ProductID
ORDER BY d.SalesOrderID, d.SalesOrderDetailID;";

        // First result set: order headers, in SELECT order.
        private const int OrdinalHeaderSalesOrderId = 0;
        private const int OrdinalHeaderOrderDate = 1;
        private const int OrdinalHeaderStatus = 2;
        private const int OrdinalHeaderCustomerName = 3;
        private const int OrdinalHeaderAccountNumber = 4;
        private const int OrdinalHeaderTotalDue = 5;

        // Second result set: order lines, in SELECT order.
        private const int OrdinalDetailSalesOrderId = 0;
        private const int OrdinalDetailProductName = 1;
        private const int OrdinalDetailProductNumber = 2;
        private const int OrdinalDetailUnitPrice = 3;
        private const int OrdinalDetailQuantity = 4;
        private const int OrdinalDetailLineTotal = 5;

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly SyncAgentOptions _options;

        public OrderQuery(ISqlConnectionFactory connectionFactory, SyncAgentOptions options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyList<OrderRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
            using (var command = SqlCommandFactory.Create(HeaderAndDetailSql, connection, _options, modifiedSince, maxRecords))
            using (var reader = await command
                       .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                       .ConfigureAwait(false))
            {
                var headers = await ReadHeadersAsync(reader, cancellationToken).ConfigureAwait(false);

                if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "The orders query did not return the order detail result set.");
                }

                var detailsByOrder = await ReadDetailsAsync(reader, cancellationToken).ConfigureAwait(false);

                return Assemble(headers, detailsByOrder);
            }
        }

        private static async Task<List<OrderHeaderRow>> ReadHeadersAsync(
            SqlDataReader reader,
            CancellationToken cancellationToken)
        {
            var headers = new List<OrderHeaderRow>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headers.Add(new OrderHeaderRow
                {
                    SalesOrderId = reader.GetInt32(OrdinalHeaderSalesOrderId),
                    OrderDate = reader.GetUtcDateTime(OrdinalHeaderOrderDate),
                    Status = reader.GetByte(OrdinalHeaderStatus),
                    CustomerName = reader.GetNullableString(OrdinalHeaderCustomerName),
                    AccountNumber = reader.GetString(OrdinalHeaderAccountNumber),
                    TotalDue = reader.GetDecimal(OrdinalHeaderTotalDue),
                });
            }

            return headers;
        }

        private static async Task<Dictionary<int, List<OrderDetailRecord>>> ReadDetailsAsync(
            SqlDataReader reader,
            CancellationToken cancellationToken)
        {
            var detailsByOrder = new Dictionary<int, List<OrderDetailRecord>>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var salesOrderId = reader.GetInt32(OrdinalDetailSalesOrderId);

                if (!detailsByOrder.TryGetValue(salesOrderId, out var lines))
                {
                    lines = new List<OrderDetailRecord>();
                    detailsByOrder.Add(salesOrderId, lines);
                }

                lines.Add(new OrderDetailRecord
                {
                    ProductName = reader.GetString(OrdinalDetailProductName),
                    ProductNumber = reader.GetString(OrdinalDetailProductNumber),
                    UnitPrice = reader.GetDecimal(OrdinalDetailUnitPrice),
                    Quantity = reader.GetInt16(OrdinalDetailQuantity),
                    LineTotal = reader.GetDecimal(OrdinalDetailLineTotal),
                });
            }

            return detailsByOrder;
        }

        private static IReadOnlyList<OrderRecord> Assemble(
            List<OrderHeaderRow> headers,
            Dictionary<int, List<OrderDetailRecord>> detailsByOrder)
        {
            var orders = new List<OrderRecord>(headers.Count);

            foreach (var header in headers)
            {
                // An order with no lines still ships, with an empty array. Dropping it would
                // hide a real data condition from the platform.
                var lines = detailsByOrder.TryGetValue(header.SalesOrderId, out var found)
                    ? (IReadOnlyList<OrderDetailRecord>)found
                    : new List<OrderDetailRecord>(0);

                orders.Add(new OrderRecord
                {
                    SalesOrderId = header.SalesOrderId,
                    OrderDate = header.OrderDate,
                    Status = header.Status,
                    CustomerName = header.CustomerName,
                    AccountNumber = header.AccountNumber,
                    TotalDue = header.TotalDue,
                    OrderDetails = lines,
                });
            }

            return orders;
        }

        /// <summary>
        /// A header row before its lines are attached. Separate from
        /// <see cref="OrderRecord"/> so the wire type can keep its collection init-only.
        /// </summary>
        private sealed class OrderHeaderRow
        {
            public int SalesOrderId { get; init; }

            public DateTime OrderDate { get; init; }

            public byte Status { get; init; }

            public string? CustomerName { get; init; }

            public string AccountNumber { get; init; } = string.Empty;

            public decimal TotalDue { get; init; }
        }
    }
}
