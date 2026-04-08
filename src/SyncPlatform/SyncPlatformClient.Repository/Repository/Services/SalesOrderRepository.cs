using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SyncPlatformClient.Repository.DataModels;
using SyncPlatformClient.Repository.Repository.Interfaces;
using System.Data;

namespace SyncPlatformClient.Repository.Repository.Services
{
    public class SalesOrderRepository : ISalesOrderRepository
    {
        private readonly IDbConnection _db;
        private readonly IConfiguration _configuration;

        public SalesOrderRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            var connectionString = _configuration.GetConnectionString("MyConnection");
            _db = new SqlConnection(connectionString);
        }

        public async Task< IEnumerable<Order>> GetOrderListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
        {
            const string sql = """
            SELECT
                ssoh.SalesOrderID                  as SalesOrderId,
            	sod.OrderQty                       as Quantity,
            	pp.FirstName + ' , ' + pp.LastName as CustomerName,
            	ppr.Name                           as ProductName,
                ssoh.OrderDate,
                ssoh.Status,
                sc.AccountNumber,
                ssoh.TotalDue,
                ppr.ProductNumber,
                sod.UnitPrice,
                sod.LineTotal
            FROM Sales.SalesOrderHeader ssoh
            JOIN Sales.Customer sc
                ON ssoh.CustomerID = sc.CustomerID
            JOIN Person.Person pp
                ON sc.PersonID = pp.BusinessEntityID
            JOIN Sales.SalesOrderDetail sod
                ON ssoh.SalesOrderID = sod.SalesOrderID
            JOIN Production.Product ppr
                ON sod.ProductID = ppr.ProductID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
            ORDER BY ssoh.SalesOrderID
            """;

            var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
            return await  _db.QueryAsync<Order>(cmd);
        }
    }
}
