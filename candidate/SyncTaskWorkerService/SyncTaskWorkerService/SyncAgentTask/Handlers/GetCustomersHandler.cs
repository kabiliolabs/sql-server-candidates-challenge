
using Dapper;
using SyncTaskWorkerService.Data;
using System.Text.Json;

namespace SyncTaskWorkerService.SyncAgentTask.Handlers
{
    public class GetCustomersHandler(IDbConnection dbConnection) : ISyncTaskHandler
    {
        public string SyncName => "GetCustomers";
        public async Task<IEnumerable<object>> HandleAsync(JsonElement parameters, CancellationToken ct)
        {
            DateTime modifiedSince = parameters.GetProperty("modifiedSince").GetDateTime();

            var connection = dbConnection.Create();

            var sql = @"
                SELECT 
                c.CustomerID,
                c.AccountNumber,
                p.FirstName,
                p.LastName,
                e.EmailAddress,
                ph.PhoneNumber       AS Phone,
                a.AddressLine1,
                a.City,
                sp.Name              AS StateProvince,
                a.PostalCode,
                cr.Name              AS CountryRegion
            FROM Sales.Customer c
            JOIN Person.Person p                       ON c.PersonID = p.BusinessEntityID
            JOIN Person.EmailAddress e            ON p.BusinessEntityID = e.BusinessEntityID
            JOIN Person.PersonPhone ph            ON p.BusinessEntityID = ph.BusinessEntityID
            JOIN Person.BusinessEntityAddress bea ON p.BusinessEntityID = bea.BusinessEntityID
            JOIN Person.Address a                 ON bea.AddressID = a.AddressID
            JOIN Person.StateProvince sp          ON a.StateProvinceID = sp.StateProvinceID
            JOIN Person.CountryRegion cr          ON sp.CountryRegionCode = cr.CountryRegionCode
            WHERE c.ModifiedDate >= @ModifiedDate";

            var result = await connection.QueryAsync(sql, new { ModifiedDate = modifiedSince });

            return result;
        }
    }
}
