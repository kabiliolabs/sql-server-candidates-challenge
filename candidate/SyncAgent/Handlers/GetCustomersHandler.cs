using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SyncAgent.Contracts;
using SyncAgent.Domain;
using SyncAgent.Protocol;
using SyncAgent.Sql;

namespace SyncAgent.Handlers
{
    public sealed class GetCustomersHandler : ISyncTaskHandler
    {
        // Fan-out (email/phone/address) capped at one row each via single-row OUTER APPLY.
        private const string Query = @"
SELECT TOP (@MaxRows) c.CustomerID, c.AccountNumber,
  COALESCE(p.FirstName, s.Name) AS FirstName,
  COALESCE(p.LastName, N'') AS LastName,
  ea.EmailAddress, ph.PhoneNumber AS Phone,
  a.AddressLine1, a.City, sp.Name AS StateProvince,
  a.PostalCode, cr.Name AS CountryRegion
FROM Sales.Customer c
LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
OUTER APPLY (SELECT TOP 1 e.EmailAddress FROM Person.EmailAddress e
             WHERE e.BusinessEntityID = p.BusinessEntityID ORDER BY e.EmailAddressID) ea
OUTER APPLY (SELECT TOP 1 pp.PhoneNumber FROM Person.PersonPhone pp
             WHERE pp.BusinessEntityID = p.BusinessEntityID ORDER BY pp.PhoneNumberTypeID) ph
OUTER APPLY (SELECT TOP 1 a2.AddressID
             FROM Person.BusinessEntityAddress bea
             JOIN Person.Address a2 ON a2.AddressID = bea.AddressID
             WHERE bea.BusinessEntityID = COALESCE(c.PersonID, c.StoreID)
             ORDER BY bea.AddressTypeID) ba
LEFT JOIN Person.Address a ON a.AddressID = ba.AddressID
LEFT JOIN Person.StateProvince sp ON sp.StateProvinceID = a.StateProvinceID
LEFT JOIN Person.CountryRegion cr ON cr.CountryRegionCode = sp.CountryRegionCode
WHERE (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
ORDER BY c.CustomerID";

        private readonly SqlConnectionFactory _connections;
        private readonly AgentOptions _options;
        private readonly ILogger<GetCustomersHandler> _logger;

        public string TaskType => "GetCustomers";

        public GetCustomersHandler(
            SqlConnectionFactory connections,
            AgentOptions options,
            ILogger<GetCustomersHandler> logger)
        {
            _connections = connections;
            _options = options;
            _logger = logger;
        }

        public async Task<SyncResult> ExecuteAsync(SyncTask task, CancellationToken cancellationToken)
        {
            DateTime? modifiedSince = task.parameters?.modifiedSince;
            var rows = new List<object>();

            using (var conn = _connections.Create())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = Query;
                cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                cmd.Parameters.Add(SqlParameterHelpers.ModifiedSince(modifiedSince));
                cmd.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = _options.MaxRows });

                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    int customerId = reader.GetOrdinal("CustomerID");
                    int accountNumber = reader.GetOrdinal("AccountNumber");
                    int firstName = reader.GetOrdinal("FirstName");
                    int lastName = reader.GetOrdinal("LastName");
                    int emailAddress = reader.GetOrdinal("EmailAddress");
                    int phone = reader.GetOrdinal("Phone");
                    int addressLine1 = reader.GetOrdinal("AddressLine1");
                    int city = reader.GetOrdinal("City");
                    int stateProvince = reader.GetOrdinal("StateProvince");
                    int postalCode = reader.GetOrdinal("PostalCode");
                    int countryRegion = reader.GetOrdinal("CountryRegion");

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new CustomerRow
                        {
                            customerId = reader.GetInt32(customerId),
                            accountNumber = reader.GetString(accountNumber),
                            firstName = reader.GetStringOrNull(firstName),
                            lastName = reader.GetStringOrNull(lastName),
                            emailAddress = reader.GetStringOrNull(emailAddress),
                            phone = reader.GetStringOrNull(phone),
                            addressLine1 = reader.GetStringOrNull(addressLine1),
                            city = reader.GetStringOrNull(city),
                            stateProvince = reader.GetStringOrNull(stateProvince),
                            postalCode = reader.GetStringOrNull(postalCode),
                            countryRegion = reader.GetStringOrNull(countryRegion)
                        });
                    }
                }
            }

            _logger.LogInformation("GetCustomers: {Count} rows (modifiedSince={ModifiedSince}).",
                rows.Count, modifiedSince.HasValue ? modifiedSince.Value.ToString("o") : "all");
            return SyncResult.Completed(task.taskId, task.taskType, rows, DateTime.UtcNow);
        }
    }
}