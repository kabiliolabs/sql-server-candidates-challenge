using Microsoft.EntityFrameworkCore;
using SyncAgent.Data.Entities;

namespace SyncAgent.Data;

/// <summary>
/// Read-only EF Core context over the subset of AdventureWorks tables needed by the Sync Agent.
/// This app never writes to AdventureWorks - no migrations, no SaveChanges usage.
/// </summary>
public class AdventureWorksDbContext(DbContextOptions<AdventureWorksDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductInventory> ProductInventories => Set<ProductInventory>();
    public DbSet<SalesOrderHeader> SalesOrderHeaders => Set<SalesOrderHeader>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customer", "Sales");
            e.HasKey(c => c.CustomerId);
            e.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId);
            e.HasOne(c => c.Store).WithMany().HasForeignKey(c => c.StoreId);
        });

        modelBuilder.Entity<Person>(e =>
        {
            e.ToTable("Person", "Person");
            e.HasKey(p => p.BusinessEntityId);
            e.HasMany(p => p.EmailAddresses).WithOne().HasForeignKey(em => em.BusinessEntityId);
            e.HasMany(p => p.PersonPhones).WithOne().HasForeignKey(pp => pp.BusinessEntityId);
            e.HasMany(p => p.BusinessEntityAddresses).WithOne().HasForeignKey(bea => bea.BusinessEntityId);
        });

        modelBuilder.Entity<EmailAddress>(e =>
        {
            e.ToTable("EmailAddress", "Person");
            e.HasKey(em => new { em.BusinessEntityId, em.EmailAddressId });
            e.Property(em => em.EmailAddress1).HasColumnName("EmailAddress");
        });

        modelBuilder.Entity<PersonPhone>(e =>
        {
            e.ToTable("PersonPhone", "Person");
            e.HasKey(pp => new { pp.BusinessEntityId, pp.PhoneNumber });
        });

        modelBuilder.Entity<BusinessEntityAddress>(e =>
        {
            e.ToTable("BusinessEntityAddress", "Person");
            e.HasKey(bea => new { bea.BusinessEntityId, bea.AddressId });
            e.HasOne(bea => bea.Address).WithMany().HasForeignKey(bea => bea.AddressId);
        });

        modelBuilder.Entity<Address>(e =>
        {
            e.ToTable("Address", "Person");
            e.HasKey(a => a.AddressId);
            e.HasOne(a => a.StateProvince).WithMany().HasForeignKey(a => a.StateProvinceId);
        });

        modelBuilder.Entity<StateProvince>(e =>
        {
            e.ToTable("StateProvince", "Person");
            e.HasKey(sp => sp.StateProvinceId);
            e.HasOne(sp => sp.CountryRegion).WithMany().HasForeignKey(sp => sp.CountryRegionCode);
        });

        modelBuilder.Entity<CountryRegion>(e =>
        {
            e.ToTable("CountryRegion", "Person");
            e.HasKey(cr => cr.CountryRegionCode);
        });

        modelBuilder.Entity<Store>(e =>
        {
            e.ToTable("Store", "Sales");
            e.HasKey(s => s.BusinessEntityId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Product", "Production");
            e.HasKey(p => p.ProductId);
            e.HasOne(p => p.ProductSubcategory).WithMany().HasForeignKey(p => p.ProductSubcategoryId);
            e.Property(p => p.StandardCost).HasColumnType("money");
            e.Property(p => p.ListPrice).HasColumnType("money");
        });

        modelBuilder.Entity<ProductSubcategory>(e =>
        {
            e.ToTable("ProductSubcategory", "Production");
            e.HasKey(ps => ps.ProductSubcategoryId);
            e.HasOne(ps => ps.ProductCategory).WithMany().HasForeignKey(ps => ps.ProductCategoryId);
        });

        modelBuilder.Entity<ProductCategory>(e =>
        {
            e.ToTable("ProductCategory", "Production");
            e.HasKey(pc => pc.ProductCategoryId);
        });

        modelBuilder.Entity<ProductInventory>(e =>
        {
            e.ToTable("ProductInventory", "Production");
            e.HasKey(pi => new { pi.ProductId, pi.LocationId });
            e.HasOne(pi => pi.Product).WithMany().HasForeignKey(pi => pi.ProductId);
            e.HasOne(pi => pi.Location).WithMany().HasForeignKey(pi => pi.LocationId);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.ToTable("Location", "Production");
            e.HasKey(l => l.LocationId);
        });

        modelBuilder.Entity<SalesOrderHeader>(e =>
        {
            e.ToTable("SalesOrderHeader", "Sales");
            e.HasKey(soh => soh.SalesOrderId);
            e.HasOne(soh => soh.Customer).WithMany(c => c.SalesOrderHeaders).HasForeignKey(soh => soh.CustomerId);
            e.HasMany(soh => soh.SalesOrderDetails).WithOne().HasForeignKey(sod => sod.SalesOrderId);
            e.Property(soh => soh.TotalDue).HasColumnType("money");
        });

        modelBuilder.Entity<SalesOrderDetail>(e =>
        {
            e.ToTable("SalesOrderDetail", "Sales");
            e.HasKey(sod => new { sod.SalesOrderId, sod.SalesOrderDetailId });
            e.HasOne(sod => sod.Product).WithMany().HasForeignKey(sod => sod.ProductId);
            e.Property(sod => sod.UnitPrice).HasColumnType("money");
            e.Property(sod => sod.LineTotal).HasColumnType("numeric(38, 6)");
        });
    }
}
