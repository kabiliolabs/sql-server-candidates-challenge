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
    public class ProductInventoryRepository : IProductInventoryRepository
    {
        private readonly IDbConnection _db;
        private readonly IConfiguration _configuration;

        public ProductInventoryRepository(IConfiguration configuration)
        {
            _configuration = configuration;
            var connectionString = _configuration.GetConnectionString("MyConnection");
            _db = new SqlConnection(connectionString);
        }

        public async Task<IEnumerable<ProductInventory>> GetProductInventoryListAsync(DateTime? modifiedSince, CancellationToken cancellationToken = default)
        {
            const string sql = """
            SELECT
                ppi.ProductID       as ProductId,
                pp.Name             as ProductName,
            	l.Name              as LocationName,
                pp.ProductNumber,
                ppi.Shelf,
                ppi.Bin,
                ppi.Quantity,
                ppi.ModifiedDate
            FROM Production.ProductInventory ppi
            JOIN Production.Product pp
                ON ppi.ProductID = pp.ProductID
            JOIN Production.Location l
                ON ppi.LocationID = l.LocationID
            WHERE (@ModifiedSince IS NULL OR pi.ModifiedDate >= @ModifiedSince)
            ORDER BY ppi.ProductID, l.LocationID
            """;

            var cmd = new CommandDefinition(sql, new { ModifiedSince = modifiedSince }, cancellationToken: cancellationToken);
            return await _db.QueryAsync<ProductInventory>(cmd);
        }
    }
}
