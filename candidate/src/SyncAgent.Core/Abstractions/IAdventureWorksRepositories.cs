using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Core.Models.Records;

namespace SyncAgent.Core.Abstractions
{
    /// <summary>
    /// Read access to the customer projection of the client database.
    /// </summary>
    /// <remarks>
    /// One interface per payload rather than one wide data-access interface. A handler then
    /// depends only on the query it actually calls, so a change to the orders query cannot
    /// force the customers handler or its tests to be touched.
    ///
    /// Every method takes an explicit <c>maxRecords</c> instead of reading a limit from
    /// configuration internally. The cap is part of the contract between handler and query,
    /// and making it a parameter is what lets a test assert that the limit reaches the SQL.
    /// </remarks>
    public interface ICustomerQuery
    {
        Task<IReadOnlyList<CustomerRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken);
    }

    /// <summary>Read access to the product catalogue.</summary>
    public interface IProductQuery
    {
        Task<IReadOnlyList<ProductRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken);
    }

    /// <summary>Read access to sales orders, with their lines.</summary>
    public interface IOrderQuery
    {
        Task<IReadOnlyList<OrderRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken);
    }

    /// <summary>Read access to stock levels per product and location.</summary>
    public interface IProductInventoryQuery
    {
        Task<IReadOnlyList<ProductInventoryRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken);
    }
}
