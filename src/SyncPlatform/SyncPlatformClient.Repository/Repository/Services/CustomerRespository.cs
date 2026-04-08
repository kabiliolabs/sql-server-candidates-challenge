using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncPlatformClient.Repository.Repository.Services
{
    public class CustomerRespository : ICustomerRepository
    {
        private readonly IDbConnection _db;
        private readonly IConfiguration _configuration;

        public CustomerRespository(IConfiguration configuration)
        {
            _configuration = configuration;
            var connectionString = _configuration.GetConnectionString("MyConnection");
            _db = new SqlConnection(connectionString);
        }

        public async Task<IEnumerable<Customer>> GetCustomerListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
        {

            const string sql = $"""
                     SELECT
                         c.CustomerID        as CustomerId,
                    	 ph.PhoneNumber      as Phone,
                    	 psp.Name             as StateProvince,
                    	 cr.Name             as CountryRegion,
                         c.AccountNumber,
                         p.FirstName,
                         p.LastName,
                         ea.EmailAddress,
                         a.AddressLine1,
                         a.City,
                         a.PostalCode
                     FROM Sales.Customer c
                      JOIN Person.Person p
                         ON c.PersonID = p.BusinessEntityID
                      JOIN Person.EmailAddress ea
                         ON p.BusinessEntityID = ea.BusinessEntityID
                      JOIN Person.PersonPhone ph
                         ON p.BusinessEntityID = ph.BusinessEntityID
                      JOIN Person.BusinessEntityAddress bea
                         ON p.BusinessEntityID = bea.BusinessEntityID
                      JOIN Person.Address a
                         ON bea.AddressID = a.AddressID
                      JOIN Person.StateProvince psp
                         ON a.StateProvinceID = psp.StateProvinceID
                      JOIN Person.CountryRegion cr
                         ON psp.CountryRegionCode = cr.CountryRegionCode
                    WHERE (@ModifiedSince IS NULL OR c.ModifiedDate >= @ModifiedSince)
                    ORDER BY c.CustomerID
                    """;

            var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
            return await _db.QueryAsync<Customer>(cmd);
        }
    }
}
