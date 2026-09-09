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
    /// <inheritdoc cref="ICustomerQuery" />
    public sealed class CustomerQuery : ICustomerQuery
    {
        /*
            Mapping notes for this projection, since none of it is obvious from the sample:

            - The contract's customer record is person-centric: firstName and lastName are not
              optional. 701 of the 19,820 rows in Sales.Customer have a null PersonID — they
              are store accounts with no individual attached — so the join to Person.Person is
              an INNER JOIN and those rows are not synced. Emitting them with null names would
              be worse: it puts unusable records into the platform and hides the fact that
              this is an open question for whoever owns the contract.

            - A person can hold more than one address (up to two in this dataset), and the
              contract has room for exactly one. Picking an arbitrary row would make the
              payload non-deterministic between runs and untestable. The OUTER APPLY orders by
              address type — Home first, matching the sample — and breaks ties on AddressID.

            - Email and phone are the same story: OUTER APPLY with TOP 1 and an explicit order,
              so re-running the sync produces the same record.

            - OUTER APPLY rather than a join, so a customer with no address, no e-mail or no
              phone still syncs with those fields null instead of vanishing from the result.

            - The cutoff tests both Customer.ModifiedDate and Person.ModifiedDate. The record
              is a projection over both tables, so it has changed if either row has. Address,
              e-mail and phone changes are deliberately not part of the predicate: adding four
              more OR'd date columns would cost the whole index strategy for a case the
              contract does not ask about.
        */
        private const string Sql = @"
SELECT TOP (@maxRecords)
    c.CustomerID,
    c.AccountNumber,
    p.FirstName,
    p.LastName,
    email.EmailAddress,
    phone.PhoneNumber,
    addr.AddressLine1,
    addr.City,
    addr.StateProvince,
    addr.PostalCode,
    addr.CountryRegion
FROM Sales.Customer AS c
INNER JOIN Person.Person AS p
    ON p.BusinessEntityID = c.PersonID
OUTER APPLY (
    SELECT TOP (1) ea.EmailAddress
    FROM Person.EmailAddress AS ea
    WHERE ea.BusinessEntityID = p.BusinessEntityID
    ORDER BY ea.EmailAddressID
) AS email
OUTER APPLY (
    SELECT TOP (1) pp.PhoneNumber
    FROM Person.PersonPhone AS pp
    WHERE pp.BusinessEntityID = p.BusinessEntityID
    ORDER BY pp.PhoneNumberTypeID, pp.PhoneNumber
) AS phone
OUTER APPLY (
    SELECT TOP (1)
        a.AddressLine1,
        a.City,
        sp.Name AS StateProvince,
        a.PostalCode,
        cr.Name AS CountryRegion
    FROM Person.BusinessEntityAddress AS bea
    INNER JOIN Person.Address AS a
        ON a.AddressID = bea.AddressID
    INNER JOIN Person.AddressType AS at
        ON at.AddressTypeID = bea.AddressTypeID
    INNER JOIN Person.StateProvince AS sp
        ON sp.StateProvinceID = a.StateProvinceID
    INNER JOIN Person.CountryRegion AS cr
        ON cr.CountryRegionCode = sp.CountryRegionCode
    WHERE bea.BusinessEntityID = p.BusinessEntityID
    ORDER BY
        CASE at.Name
            WHEN 'Home'        THEN 1
            WHEN 'Main Office' THEN 2
            WHEN 'Billing'     THEN 3
            WHEN 'Primary'     THEN 4
            WHEN 'Shipping'    THEN 5
            ELSE 6
        END,
        a.AddressID
) AS addr
WHERE c.ModifiedDate >= @modifiedSince
   OR p.ModifiedDate >= @modifiedSince
ORDER BY c.CustomerID;";

        // Ordinals in SELECT order. The reader runs in SequentialAccess mode, so columns must
        // be consumed left to right and never revisited.
        private const int OrdinalCustomerId = 0;
        private const int OrdinalAccountNumber = 1;
        private const int OrdinalFirstName = 2;
        private const int OrdinalLastName = 3;
        private const int OrdinalEmailAddress = 4;
        private const int OrdinalPhone = 5;
        private const int OrdinalAddressLine1 = 6;
        private const int OrdinalCity = 7;
        private const int OrdinalStateProvince = 8;
        private const int OrdinalPostalCode = 9;
        private const int OrdinalCountryRegion = 10;

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly SyncAgentOptions _options;

        public CustomerQuery(ISqlConnectionFactory connectionFactory, SyncAgentOptions options)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IReadOnlyList<CustomerRecord>> GetModifiedSinceAsync(
            DateTimeOffset modifiedSince,
            int maxRecords,
            CancellationToken cancellationToken)
        {
            var records = new List<CustomerRecord>();

            using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
            using (var command = SqlCommandFactory.Create(Sql, connection, _options, modifiedSince, maxRecords))
            using (var reader = await command
                       .ExecuteReaderAsync(
                           CommandBehavior.SequentialAccess | CommandBehavior.SingleResult,
                           cancellationToken)
                       .ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    records.Add(new CustomerRecord
                    {
                        CustomerId = reader.GetInt32(OrdinalCustomerId),
                        AccountNumber = reader.GetString(OrdinalAccountNumber),
                        FirstName = reader.GetString(OrdinalFirstName),
                        LastName = reader.GetString(OrdinalLastName),
                        EmailAddress = reader.GetNullableString(OrdinalEmailAddress),
                        Phone = reader.GetNullableString(OrdinalPhone),
                        AddressLine1 = reader.GetNullableString(OrdinalAddressLine1),
                        City = reader.GetNullableString(OrdinalCity),
                        StateProvince = reader.GetNullableString(OrdinalStateProvince),
                        PostalCode = reader.GetNullableString(OrdinalPostalCode),
                        CountryRegion = reader.GetNullableString(OrdinalCountryRegion),
                    });
                }
            }

            return records;
        }
    }
}
