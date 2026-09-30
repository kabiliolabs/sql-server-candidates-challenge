using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SyncAgent.Core.Models;
using SyncAgent.Infrastructure.Data;
using SyncAgent.Infrastructure.Options;

namespace SyncAgent.Infrastructure.Handlers.Customers
{
    public sealed class GetCustomersHandler(
        IDbConnectionFactory connectionFactory,
        IOptions<SyncOptions> options,
        ILogger<GetCustomersHandler> logger) : SqlQueryHandler<CustomerRecord>(connectionFactory, options, logger)
    {
        public override string TaskType => SyncTaskTypes.GetCustomers;

        protected override string Sql => """
            SELECT TOP (@MaxRecords)
                c.CustomerID     AS CustomerId,
                c.AccountNumber  AS AccountNumber,
                p.FirstName      AS FirstName,
                p.LastName       AS LastName,
                em.EmailAddress  AS EmailAddress,
                ph.PhoneNumber   AS Phone,
                ad.AddressLine1  AS AddressLine1,
                ad.City          AS City,
                ad.StateProvince AS StateProvince,
                ad.PostalCode    AS PostalCode,
                ad.CountryRegion AS CountryRegion
            FROM Sales.Customer c
            JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            OUTER APPLY (SELECT TOP 1 e.EmailAddress
                         FROM Person.EmailAddress e
                         WHERE e.BusinessEntityID = p.BusinessEntityID
                         ORDER BY e.EmailAddressID) em
            OUTER APPLY (SELECT TOP 1 pp.PhoneNumber
                         FROM Person.PersonPhone pp
                         WHERE pp.BusinessEntityID = p.BusinessEntityID
                         ORDER BY pp.PhoneNumberTypeID) ph
            OUTER APPLY (SELECT TOP 1 a.AddressLine1, a.City, sp.Name AS StateProvince,
                                a.PostalCode, cr.Name AS CountryRegion
                         FROM Person.BusinessEntityAddress bea
                         JOIN Person.Address a        ON a.AddressID = bea.AddressID
                         JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
                         JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
                         WHERE bea.BusinessEntityID = p.BusinessEntityID
                         ORDER BY bea.AddressTypeID) ad
            WHERE c.ModifiedDate >= @ModifiedSince
            ORDER BY c.CustomerID;
            """;
    }
}