using System;
using System.Collections.Generic;
using AdventureWorks.Ef.Models;
using Microsoft.EntityFrameworkCore;

namespace AdventureWorks.Ef.Data;

public partial class AdventureWorksDbContext : DbContext
{
    public AdventureWorksDbContext(DbContextOptions<AdventureWorksDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Address { get; set; }

    public virtual DbSet<BillOfMaterials> BillOfMaterials { get; set; }

    public virtual DbSet<BusinessEntity> BusinessEntity { get; set; }

    public virtual DbSet<BusinessEntityAddress> BusinessEntityAddress { get; set; }

    public virtual DbSet<CountryRegion> CountryRegion { get; set; }

    public virtual DbSet<Culture> Culture { get; set; }

    public virtual DbSet<Customer> Customer { get; set; }

    public virtual DbSet<Document> Document { get; set; }

    public virtual DbSet<EmailAddress> EmailAddress { get; set; }

    public virtual DbSet<Illustration> Illustration { get; set; }

    public virtual DbSet<Location> Location { get; set; }

    public virtual DbSet<Person> Person { get; set; }

    public virtual DbSet<PersonPhone> PersonPhone { get; set; }

    public virtual DbSet<Product> Product { get; set; }

    public virtual DbSet<ProductCategory> ProductCategory { get; set; }

    public virtual DbSet<ProductCostHistory> ProductCostHistory { get; set; }

    public virtual DbSet<ProductDescription> ProductDescription { get; set; }

    public virtual DbSet<ProductDocument> ProductDocument { get; set; }

    public virtual DbSet<ProductInventory> ProductInventory { get; set; }

    public virtual DbSet<ProductListPriceHistory> ProductListPriceHistory { get; set; }

    public virtual DbSet<ProductModel> ProductModel { get; set; }

    public virtual DbSet<ProductModelIllustration> ProductModelIllustration { get; set; }

    public virtual DbSet<ProductModelProductDescriptionCulture> ProductModelProductDescriptionCulture { get; set; }

    public virtual DbSet<ProductPhoto> ProductPhoto { get; set; }

    public virtual DbSet<ProductProductPhoto> ProductProductPhoto { get; set; }

    public virtual DbSet<ProductReview> ProductReview { get; set; }

    public virtual DbSet<ProductSubcategory> ProductSubcategory { get; set; }

    public virtual DbSet<SalesOrderDetail> SalesOrderDetail { get; set; }

    public virtual DbSet<SalesOrderHeader> SalesOrderHeader { get; set; }

    public virtual DbSet<ScrapReason> ScrapReason { get; set; }

    public virtual DbSet<StateProvince> StateProvince { get; set; }

    public virtual DbSet<Store> Store { get; set; }

    public virtual DbSet<TransactionHistory> TransactionHistory { get; set; }

    public virtual DbSet<TransactionHistoryArchive> TransactionHistoryArchive { get; set; }

    public virtual DbSet<UnitMeasure> UnitMeasure { get; set; }

    public virtual DbSet<VProductAndDescription> VProductAndDescription { get; set; }

    public virtual DbSet<VProductModelCatalogDescription> VProductModelCatalogDescription { get; set; }

    public virtual DbSet<VProductModelInstructions> VProductModelInstructions { get; set; }

    public virtual DbSet<WorkOrder> WorkOrder { get; set; }

    public virtual DbSet<WorkOrderRouting> WorkOrderRouting { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.AddressId).HasName("PK_Address_AddressID");

            entity.ToTable("Address", "Person", tb => tb.HasComment("Street address information for customers, employees, and vendors."));

            entity.HasIndex(e => e.Rowguid, "AK_Address_rowguid").IsUnique();

            entity.HasIndex(e => new { e.AddressLine1, e.AddressLine2, e.City, e.StateProvinceId, e.PostalCode }, "IX_Address_AddressLine1_AddressLine2_City_StateProvinceID_PostalCode").IsUnique();

            entity.HasIndex(e => e.StateProvinceId, "IX_Address_StateProvinceID");

            entity.Property(e => e.AddressId)
                .HasComment("Primary key for Address records.")
                .HasColumnName("AddressID");
            entity.Property(e => e.AddressLine1)
                .HasMaxLength(60)
                .HasComment("First street address line.");
            entity.Property(e => e.AddressLine2)
                .HasMaxLength(60)
                .HasComment("Second street address line.");
            entity.Property(e => e.City)
                .HasMaxLength(30)
                .HasComment("Name of the city.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Address_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.PostalCode)
                .HasMaxLength(15)
                .HasComment("Postal code for the street address.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_Address_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.StateProvinceId)
                .HasComment("Unique identification number for the state or province. Foreign key to StateProvince table.")
                .HasColumnName("StateProvinceID");

            entity.HasOne(d => d.StateProvince).WithMany(p => p.Address)
                .HasForeignKey(d => d.StateProvinceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<BillOfMaterials>(entity =>
        {
            entity.HasKey(e => e.BillOfMaterialsId)
                .HasName("PK_BillOfMaterials_BillOfMaterialsID")
                .IsClustered(false);

            entity.ToTable("BillOfMaterials", "Production", tb => tb.HasComment("Items required to make bicycles and bicycle subassemblies. It identifies the heirarchical relationship between a parent product and its components."));

            entity.HasIndex(e => new { e.ProductAssemblyId, e.ComponentId, e.StartDate }, "AK_BillOfMaterials_ProductAssemblyID_ComponentID_StartDate")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.UnitMeasureCode, "IX_BillOfMaterials_UnitMeasureCode");

            entity.Property(e => e.BillOfMaterialsId)
                .HasComment("Primary key for BillOfMaterials records.")
                .HasColumnName("BillOfMaterialsID");
            entity.Property(e => e.Bomlevel)
                .HasComment("Indicates the depth the component is from its parent (AssemblyID).")
                .HasColumnName("BOMLevel");
            entity.Property(e => e.ComponentId)
                .HasComment("Component identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ComponentID");
            entity.Property(e => e.EndDate)
                .HasComment("Date the component stopped being used in the assembly item.")
                .HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_BillOfMaterials_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.PerAssemblyQty)
                .HasComment("Quantity of the component needed to create the assembly.")
                .HasDefaultValue(1.00m, "DF_BillOfMaterials_PerAssemblyQty")
                .HasColumnType("decimal(8, 2)");
            entity.Property(e => e.ProductAssemblyId)
                .HasComment("Parent product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductAssemblyID");
            entity.Property(e => e.StartDate)
                .HasComment("Date the component started being used in the assembly item.")
                .HasDefaultValueSql("(getdate())", "DF_BillOfMaterials_StartDate")
                .HasColumnType("datetime");
            entity.Property(e => e.UnitMeasureCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasComment("Standard code identifying the unit of measure for the quantity.");

            entity.HasOne(d => d.Component).WithMany(p => p.BillOfMaterialsComponent)
                .HasForeignKey(d => d.ComponentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProductAssembly).WithMany(p => p.BillOfMaterialsProductAssembly).HasForeignKey(d => d.ProductAssemblyId);

            entity.HasOne(d => d.UnitMeasureCodeNavigation).WithMany(p => p.BillOfMaterials)
                .HasForeignKey(d => d.UnitMeasureCode)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<BusinessEntity>(entity =>
        {
            entity.HasKey(e => e.BusinessEntityId).HasName("PK_BusinessEntity_BusinessEntityID");

            entity.ToTable("BusinessEntity", "Person", tb => tb.HasComment("Source of the ID that connects vendors, customers, and employees with address and contact information."));

            entity.HasIndex(e => e.Rowguid, "AK_BusinessEntity_rowguid").IsUnique();

            entity.Property(e => e.BusinessEntityId)
                .HasComment("Primary key for all customers, vendors, and employees.")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_BusinessEntity_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_BusinessEntity_rowguid")
                .HasColumnName("rowguid");
        });

        modelBuilder.Entity<BusinessEntityAddress>(entity =>
        {
            entity.HasKey(e => new { e.BusinessEntityId, e.AddressId, e.AddressTypeId }).HasName("PK_BusinessEntityAddress_BusinessEntityID_AddressID_AddressTypeID");

            entity.ToTable("BusinessEntityAddress", "Person", tb => tb.HasComment("Cross-reference table mapping customers, vendors, and employees to their addresses."));

            entity.HasIndex(e => e.Rowguid, "AK_BusinessEntityAddress_rowguid").IsUnique();

            entity.HasIndex(e => e.AddressId, "IX_BusinessEntityAddress_AddressID");

            entity.HasIndex(e => e.AddressTypeId, "IX_BusinessEntityAddress_AddressTypeID");

            entity.Property(e => e.BusinessEntityId)
                .HasComment("Primary key. Foreign key to BusinessEntity.BusinessEntityID.")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.AddressId)
                .HasComment("Primary key. Foreign key to Address.AddressID.")
                .HasColumnName("AddressID");
            entity.Property(e => e.AddressTypeId)
                .HasComment("Primary key. Foreign key to AddressType.AddressTypeID.")
                .HasColumnName("AddressTypeID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_BusinessEntityAddress_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_BusinessEntityAddress_rowguid")
                .HasColumnName("rowguid");

            entity.HasOne(d => d.Address).WithMany(p => p.BusinessEntityAddress)
                .HasForeignKey(d => d.AddressId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.BusinessEntity).WithMany(p => p.BusinessEntityAddress)
                .HasForeignKey(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<CountryRegion>(entity =>
        {
            entity.HasKey(e => e.CountryRegionCode).HasName("PK_CountryRegion_CountryRegionCode");

            entity.ToTable("CountryRegion", "Person", tb => tb.HasComment("Lookup table containing the ISO standard codes for countries and regions."));

            entity.HasIndex(e => e.Name, "AK_CountryRegion_Name").IsUnique();

            entity.Property(e => e.CountryRegionCode)
                .HasMaxLength(3)
                .HasComment("ISO standard code for countries and regions.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_CountryRegion_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Country or region name.");
        });

        modelBuilder.Entity<Culture>(entity =>
        {
            entity.HasKey(e => e.CultureId).HasName("PK_Culture_CultureID");

            entity.ToTable("Culture", "Production", tb => tb.HasComment("Lookup table containing the languages in which some AdventureWorks data is stored."));

            entity.HasIndex(e => e.Name, "AK_Culture_Name").IsUnique();

            entity.Property(e => e.CultureId)
                .HasMaxLength(6)
                .IsFixedLength()
                .HasComment("Primary key for Culture records.")
                .HasColumnName("CultureID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Culture_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Culture description.");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK_Customer_CustomerID");

            entity.ToTable("Customer", "Sales", tb => tb.HasComment("Current customer information. Also see the Person and Store tables."));

            entity.HasIndex(e => e.AccountNumber, "AK_Customer_AccountNumber").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_Customer_rowguid").IsUnique();

            entity.HasIndex(e => e.TerritoryId, "IX_Customer_TerritoryID");

            entity.Property(e => e.CustomerId)
                .HasComment("Primary key.")
                .HasColumnName("CustomerID");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasComputedColumnSql("(isnull('AW'+[dbo].[ufnLeadingZeros]([CustomerID]),''))", false)
                .HasComment("Unique number identifying the customer assigned by the accounting system.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Customer_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.PersonId)
                .HasComment("Foreign key to Person.BusinessEntityID")
                .HasColumnName("PersonID");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_Customer_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.StoreId)
                .HasComment("Foreign key to Store.BusinessEntityID")
                .HasColumnName("StoreID");
            entity.Property(e => e.TerritoryId)
                .HasComment("ID of the territory in which the customer is located. Foreign key to SalesTerritory.SalesTerritoryID.")
                .HasColumnName("TerritoryID");

            entity.HasOne(d => d.Person).WithMany(p => p.Customer).HasForeignKey(d => d.PersonId);

            entity.HasOne(d => d.Store).WithMany(p => p.Customer).HasForeignKey(d => d.StoreId);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentNode).HasName("PK_Document_DocumentNode");

            entity.ToTable("Document", "Production", tb => tb.HasComment("Product maintenance documents."));

            entity.HasIndex(e => new { e.DocumentLevel, e.DocumentNode }, "AK_Document_DocumentLevel_DocumentNode").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_Document_rowguid").IsUnique();

            entity.HasIndex(e => new { e.FileName, e.Revision }, "IX_Document_FileName_Revision");

            entity.HasIndex(e => e.Rowguid, "UQ__Document__F73921F7248A8848").IsUnique();

            entity.Property(e => e.DocumentNode).HasComment("Primary key for Document records.");
            entity.Property(e => e.ChangeNumber).HasComment("Engineering change approval number.");
            entity.Property(e => e.Document1)
                .HasComment("Complete document.")
                .HasColumnName("Document");
            entity.Property(e => e.DocumentLevel)
                .HasComputedColumnSql("([DocumentNode].[GetLevel]())", false)
                .HasComment("Depth in the document hierarchy.");
            entity.Property(e => e.DocumentSummary).HasComment("Document abstract.");
            entity.Property(e => e.FileExtension)
                .HasMaxLength(8)
                .HasComment("File extension indicating the document type. For example, .doc or .txt.");
            entity.Property(e => e.FileName)
                .HasMaxLength(400)
                .HasComment("File name of the document");
            entity.Property(e => e.FolderFlag).HasComment("0 = This is a folder, 1 = This is a document.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Document_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Owner).HasComment("Employee who controls the document.  Foreign key to Employee.BusinessEntityID");
            entity.Property(e => e.Revision)
                .HasMaxLength(5)
                .IsFixedLength()
                .HasComment("Revision number of the document. ");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Required for FileStream.")
                .HasDefaultValueSql("(newid())", "DF_Document_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.Status).HasComment("1 = Pending approval, 2 = Approved, 3 = Obsolete");
            entity.Property(e => e.Title)
                .HasMaxLength(50)
                .HasComment("Title of the document.");
        });

        modelBuilder.Entity<EmailAddress>(entity =>
        {
            entity.HasKey(e => new { e.BusinessEntityId, e.EmailAddressId }).HasName("PK_EmailAddress_BusinessEntityID_EmailAddressID");

            entity.ToTable("EmailAddress", "Person", tb => tb.HasComment("Where to send a person email."));

            entity.HasIndex(e => e.EmailAddress1, "IX_EmailAddress_EmailAddress");

            entity.Property(e => e.BusinessEntityId)
                .HasComment("Primary key. Person associated with this email address.  Foreign key to Person.BusinessEntityID")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.EmailAddressId)
                .ValueGeneratedOnAdd()
                .HasComment("Primary key. ID of this email address.")
                .HasColumnName("EmailAddressID");
            entity.Property(e => e.EmailAddress1)
                .HasMaxLength(50)
                .HasComment("E-mail address for the person.")
                .HasColumnName("EmailAddress");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_EmailAddress_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_EmailAddress_rowguid")
                .HasColumnName("rowguid");

            entity.HasOne(d => d.BusinessEntity).WithMany(p => p.EmailAddress)
                .HasForeignKey(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Illustration>(entity =>
        {
            entity.HasKey(e => e.IllustrationId).HasName("PK_Illustration_IllustrationID");

            entity.ToTable("Illustration", "Production", tb => tb.HasComment("Bicycle assembly diagrams."));

            entity.Property(e => e.IllustrationId)
                .HasComment("Primary key for Illustration records.")
                .HasColumnName("IllustrationID");
            entity.Property(e => e.Diagram)
                .HasComment("Illustrations used in manufacturing instructions. Stored as XML.")
                .HasColumnType("xml");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Illustration_ModifiedDate")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("PK_Location_LocationID");

            entity.ToTable("Location", "Production", tb => tb.HasComment("Product inventory and manufacturing locations."));

            entity.HasIndex(e => e.Name, "AK_Location_Name").IsUnique();

            entity.Property(e => e.LocationId)
                .HasComment("Primary key for Location records.")
                .HasColumnName("LocationID");
            entity.Property(e => e.Availability)
                .HasComment("Work capacity (in hours) of the manufacturing location.")
                .HasColumnType("decimal(8, 2)");
            entity.Property(e => e.CostRate)
                .HasComment("Standard hourly cost of the manufacturing location.")
                .HasColumnType("smallmoney");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Location_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Location description.");
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.BusinessEntityId).HasName("PK_Person_BusinessEntityID");

            entity.ToTable("Person", "Person", tb =>
                {
                    tb.HasComment("Human beings involved with AdventureWorks: employees, customer contacts, and vendor contacts.");
                    tb.HasTrigger("iuPerson");
                });

            entity.HasIndex(e => e.Rowguid, "AK_Person_rowguid").IsUnique();

            entity.HasIndex(e => new { e.LastName, e.FirstName, e.MiddleName }, "IX_Person_LastName_FirstName_MiddleName");

            entity.HasIndex(e => e.AdditionalContactInfo, "PXML_Person_AddContact");

            entity.HasIndex(e => e.Demographics, "PXML_Person_Demographics");

            entity.HasIndex(e => e.Demographics, "XMLPATH_Person_Demographics");

            entity.HasIndex(e => e.Demographics, "XMLPROPERTY_Person_Demographics");

            entity.HasIndex(e => e.Demographics, "XMLVALUE_Person_Demographics");

            entity.Property(e => e.BusinessEntityId)
                .ValueGeneratedNever()
                .HasComment("Primary key for Person records.")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.AdditionalContactInfo)
                .HasComment("Additional contact information about the person stored in xml format. ")
                .HasColumnType("xml");
            entity.Property(e => e.Demographics)
                .HasComment("Personal information such as hobbies, and income collected from online shoppers. Used for sales analysis.")
                .HasColumnType("xml");
            entity.Property(e => e.EmailPromotion).HasComment("0 = Contact does not wish to receive e-mail promotions, 1 = Contact does wish to receive e-mail promotions from AdventureWorks, 2 = Contact does wish to receive e-mail promotions from AdventureWorks and selected partners. ");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasComment("First name of the person.");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasComment("Last name of the person.");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(50)
                .HasComment("Middle name or middle initial of the person.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Person_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.NameStyle).HasComment("0 = The data in FirstName and LastName are stored in western style (first name, last name) order.  1 = Eastern style (last name, first name) order.");
            entity.Property(e => e.PersonType)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("Primary type of person: SC = Store Contact, IN = Individual (retail) customer, SP = Sales person, EM = Employee (non-sales), VC = Vendor contact, GC = General contact");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_Person_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.Suffix)
                .HasMaxLength(10)
                .HasComment("Surname suffix. For example, Sr. or Jr.");
            entity.Property(e => e.Title)
                .HasMaxLength(8)
                .HasComment("A courtesy title. For example, Mr. or Ms.");

            entity.HasOne(d => d.BusinessEntity).WithOne(p => p.Person)
                .HasForeignKey<Person>(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PersonPhone>(entity =>
        {
            entity.HasKey(e => new { e.BusinessEntityId, e.PhoneNumber, e.PhoneNumberTypeId }).HasName("PK_PersonPhone_BusinessEntityID_PhoneNumber_PhoneNumberTypeID");

            entity.ToTable("PersonPhone", "Person", tb => tb.HasComment("Telephone number and type of a person."));

            entity.HasIndex(e => e.PhoneNumber, "IX_PersonPhone_PhoneNumber");

            entity.Property(e => e.BusinessEntityId)
                .HasComment("Business entity identification number. Foreign key to Person.BusinessEntityID.")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(25)
                .HasComment("Telephone number identification number.");
            entity.Property(e => e.PhoneNumberTypeId)
                .HasComment("Kind of phone number. Foreign key to PhoneNumberType.PhoneNumberTypeID.")
                .HasColumnName("PhoneNumberTypeID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_PersonPhone_ModifiedDate")
                .HasColumnType("datetime");

            entity.HasOne(d => d.BusinessEntity).WithMany(p => p.PersonPhone)
                .HasForeignKey(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK_Product_ProductID");

            entity.ToTable("Product", "Production", tb => tb.HasComment("Products sold or used in the manfacturing of sold products."));

            entity.HasIndex(e => e.Name, "AK_Product_Name").IsUnique();

            entity.HasIndex(e => e.ProductNumber, "AK_Product_ProductNumber").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_Product_rowguid").IsUnique();

            entity.Property(e => e.ProductId)
                .HasComment("Primary key for Product records.")
                .HasColumnName("ProductID");
            entity.Property(e => e.Class)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("H = High, M = Medium, L = Low");
            entity.Property(e => e.Color)
                .HasMaxLength(15)
                .HasComment("Product color.");
            entity.Property(e => e.DaysToManufacture).HasComment("Number of days required to manufacture the product.");
            entity.Property(e => e.DiscontinuedDate)
                .HasComment("Date the product was discontinued.")
                .HasColumnType("datetime");
            entity.Property(e => e.FinishedGoodsFlag)
                .HasComment("0 = Product is not a salable item. 1 = Product is salable.")
                .HasDefaultValue(true, "DF_Product_FinishedGoodsFlag");
            entity.Property(e => e.ListPrice)
                .HasComment("Selling price.")
                .HasColumnType("money");
            entity.Property(e => e.MakeFlag)
                .HasComment("0 = Product is purchased, 1 = Product is manufactured in-house.")
                .HasDefaultValue(true, "DF_Product_MakeFlag");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Product_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Name of the product.");
            entity.Property(e => e.ProductLine)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("R = Road, M = Mountain, T = Touring, S = Standard");
            entity.Property(e => e.ProductModelId)
                .HasComment("Product is a member of this product model. Foreign key to ProductModel.ProductModelID.")
                .HasColumnName("ProductModelID");
            entity.Property(e => e.ProductNumber)
                .HasMaxLength(25)
                .HasComment("Unique product identification number.");
            entity.Property(e => e.ProductSubcategoryId)
                .HasComment("Product is a member of this product subcategory. Foreign key to ProductSubCategory.ProductSubCategoryID. ")
                .HasColumnName("ProductSubcategoryID");
            entity.Property(e => e.ReorderPoint).HasComment("Inventory level that triggers a purchase order or work order. ");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_Product_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.SafetyStockLevel).HasComment("Minimum inventory quantity. ");
            entity.Property(e => e.SellEndDate)
                .HasComment("Date the product was no longer available for sale.")
                .HasColumnType("datetime");
            entity.Property(e => e.SellStartDate)
                .HasComment("Date the product was available for sale.")
                .HasColumnType("datetime");
            entity.Property(e => e.Size)
                .HasMaxLength(5)
                .HasComment("Product size.");
            entity.Property(e => e.SizeUnitMeasureCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasComment("Unit of measure for Size column.");
            entity.Property(e => e.StandardCost)
                .HasComment("Standard cost of the product.")
                .HasColumnType("money");
            entity.Property(e => e.Style)
                .HasMaxLength(2)
                .IsFixedLength()
                .HasComment("W = Womens, M = Mens, U = Universal");
            entity.Property(e => e.Weight)
                .HasComment("Product weight.")
                .HasColumnType("decimal(8, 2)");
            entity.Property(e => e.WeightUnitMeasureCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasComment("Unit of measure for Weight column.");

            entity.HasOne(d => d.ProductModel).WithMany(p => p.Product).HasForeignKey(d => d.ProductModelId);

            entity.HasOne(d => d.ProductSubcategory).WithMany(p => p.Product).HasForeignKey(d => d.ProductSubcategoryId);

            entity.HasOne(d => d.SizeUnitMeasureCodeNavigation).WithMany(p => p.ProductSizeUnitMeasureCodeNavigation).HasForeignKey(d => d.SizeUnitMeasureCode);

            entity.HasOne(d => d.WeightUnitMeasureCodeNavigation).WithMany(p => p.ProductWeightUnitMeasureCodeNavigation).HasForeignKey(d => d.WeightUnitMeasureCode);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.ProductCategoryId).HasName("PK_ProductCategory_ProductCategoryID");

            entity.ToTable("ProductCategory", "Production", tb => tb.HasComment("High-level product categorization."));

            entity.HasIndex(e => e.Name, "AK_ProductCategory_Name").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_ProductCategory_rowguid").IsUnique();

            entity.Property(e => e.ProductCategoryId)
                .HasComment("Primary key for ProductCategory records.")
                .HasColumnName("ProductCategoryID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductCategory_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Category description.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_ProductCategory_rowguid")
                .HasColumnName("rowguid");
        });

        modelBuilder.Entity<ProductCostHistory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.StartDate }).HasName("PK_ProductCostHistory_ProductID_StartDate");

            entity.ToTable("ProductCostHistory", "Production", tb => tb.HasComment("Changes in the cost of a product over time."));

            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID")
                .HasColumnName("ProductID");
            entity.Property(e => e.StartDate)
                .HasComment("Product cost start date.")
                .HasColumnType("datetime");
            entity.Property(e => e.EndDate)
                .HasComment("Product cost end date.")
                .HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductCostHistory_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.StandardCost)
                .HasComment("Standard cost of the product.")
                .HasColumnType("money");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductCostHistory)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductDescription>(entity =>
        {
            entity.HasKey(e => e.ProductDescriptionId).HasName("PK_ProductDescription_ProductDescriptionID");

            entity.ToTable("ProductDescription", "Production", tb => tb.HasComment("Product descriptions in several languages."));

            entity.HasIndex(e => e.Rowguid, "AK_ProductDescription_rowguid").IsUnique();

            entity.Property(e => e.ProductDescriptionId)
                .HasComment("Primary key for ProductDescription records.")
                .HasColumnName("ProductDescriptionID");
            entity.Property(e => e.Description)
                .HasMaxLength(400)
                .HasComment("Description of the product.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductDescription_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_ProductDescription_rowguid")
                .HasColumnName("rowguid");
        });

        modelBuilder.Entity<ProductDocument>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.DocumentNode }).HasName("PK_ProductDocument_ProductID_DocumentNode");

            entity.ToTable("ProductDocument", "Production", tb => tb.HasComment("Cross-reference table mapping products to related product documents."));

            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.DocumentNode).HasComment("Document identification number. Foreign key to Document.DocumentNode.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductDocument_ModifiedDate")
                .HasColumnType("datetime");

            entity.HasOne(d => d.DocumentNodeNavigation).WithMany(p => p.ProductDocument)
                .HasForeignKey(d => d.DocumentNode)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductDocument)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductInventory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.LocationId }).HasName("PK_ProductInventory_ProductID_LocationID");

            entity.ToTable("ProductInventory", "Production", tb => tb.HasComment("Product inventory information."));

            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.LocationId)
                .HasComment("Inventory location identification number. Foreign key to Location.LocationID. ")
                .HasColumnName("LocationID");
            entity.Property(e => e.Bin).HasComment("Storage container on a shelf in an inventory location.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductInventory_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasComment("Quantity of products in the inventory location.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_ProductInventory_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.Shelf)
                .HasMaxLength(10)
                .HasComment("Storage compartment within an inventory location.");

            entity.HasOne(d => d.Location).WithMany(p => p.ProductInventory)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductInventory)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductListPriceHistory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.StartDate }).HasName("PK_ProductListPriceHistory_ProductID_StartDate");

            entity.ToTable("ProductListPriceHistory", "Production", tb => tb.HasComment("Changes in the list price of a product over time."));

            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID")
                .HasColumnName("ProductID");
            entity.Property(e => e.StartDate)
                .HasComment("List price start date.")
                .HasColumnType("datetime");
            entity.Property(e => e.EndDate)
                .HasComment("List price end date")
                .HasColumnType("datetime");
            entity.Property(e => e.ListPrice)
                .HasComment("Product list price.")
                .HasColumnType("money");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductListPriceHistory_ModifiedDate")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductListPriceHistory)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductModel>(entity =>
        {
            entity.HasKey(e => e.ProductModelId).HasName("PK_ProductModel_ProductModelID");

            entity.ToTable("ProductModel", "Production", tb => tb.HasComment("Product model classification."));

            entity.HasIndex(e => e.Name, "AK_ProductModel_Name").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_ProductModel_rowguid").IsUnique();

            entity.HasIndex(e => e.CatalogDescription, "PXML_ProductModel_CatalogDescription");

            entity.HasIndex(e => e.Instructions, "PXML_ProductModel_Instructions");

            entity.Property(e => e.ProductModelId)
                .HasComment("Primary key for ProductModel records.")
                .HasColumnName("ProductModelID");
            entity.Property(e => e.CatalogDescription)
                .HasComment("Detailed product catalog information in xml format.")
                .HasColumnType("xml");
            entity.Property(e => e.Instructions)
                .HasComment("Manufacturing instructions in xml format.")
                .HasColumnType("xml");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductModel_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Product model description.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_ProductModel_rowguid")
                .HasColumnName("rowguid");
        });

        modelBuilder.Entity<ProductModelIllustration>(entity =>
        {
            entity.HasKey(e => new { e.ProductModelId, e.IllustrationId }).HasName("PK_ProductModelIllustration_ProductModelID_IllustrationID");

            entity.ToTable("ProductModelIllustration", "Production", tb => tb.HasComment("Cross-reference table mapping product models and illustrations."));

            entity.Property(e => e.ProductModelId)
                .HasComment("Primary key. Foreign key to ProductModel.ProductModelID.")
                .HasColumnName("ProductModelID");
            entity.Property(e => e.IllustrationId)
                .HasComment("Primary key. Foreign key to Illustration.IllustrationID.")
                .HasColumnName("IllustrationID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductModelIllustration_ModifiedDate")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Illustration).WithMany(p => p.ProductModelIllustration)
                .HasForeignKey(d => d.IllustrationId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProductModel).WithMany(p => p.ProductModelIllustration)
                .HasForeignKey(d => d.ProductModelId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductModelProductDescriptionCulture>(entity =>
        {
            entity.HasKey(e => new { e.ProductModelId, e.ProductDescriptionId, e.CultureId }).HasName("PK_ProductModelProductDescriptionCulture_ProductModelID_ProductDescriptionID_CultureID");

            entity.ToTable("ProductModelProductDescriptionCulture", "Production", tb => tb.HasComment("Cross-reference table mapping product descriptions and the language the description is written in."));

            entity.Property(e => e.ProductModelId)
                .HasComment("Primary key. Foreign key to ProductModel.ProductModelID.")
                .HasColumnName("ProductModelID");
            entity.Property(e => e.ProductDescriptionId)
                .HasComment("Primary key. Foreign key to ProductDescription.ProductDescriptionID.")
                .HasColumnName("ProductDescriptionID");
            entity.Property(e => e.CultureId)
                .HasMaxLength(6)
                .IsFixedLength()
                .HasComment("Culture identification number. Foreign key to Culture.CultureID.")
                .HasColumnName("CultureID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductModelProductDescriptionCulture_ModifiedDate")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Culture).WithMany(p => p.ProductModelProductDescriptionCulture)
                .HasForeignKey(d => d.CultureId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProductDescription).WithMany(p => p.ProductModelProductDescriptionCulture)
                .HasForeignKey(d => d.ProductDescriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProductModel).WithMany(p => p.ProductModelProductDescriptionCulture)
                .HasForeignKey(d => d.ProductModelId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductPhoto>(entity =>
        {
            entity.HasKey(e => e.ProductPhotoId).HasName("PK_ProductPhoto_ProductPhotoID");

            entity.ToTable("ProductPhoto", "Production", tb => tb.HasComment("Product images."));

            entity.Property(e => e.ProductPhotoId)
                .HasComment("Primary key for ProductPhoto records.")
                .HasColumnName("ProductPhotoID");
            entity.Property(e => e.LargePhoto).HasComment("Large image of the product.");
            entity.Property(e => e.LargePhotoFileName)
                .HasMaxLength(50)
                .HasComment("Large image file name.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductPhoto_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ThumbNailPhoto).HasComment("Small image of the product.");
            entity.Property(e => e.ThumbnailPhotoFileName)
                .HasMaxLength(50)
                .HasComment("Small image file name.");
        });

        modelBuilder.Entity<ProductProductPhoto>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.ProductPhotoId })
                .HasName("PK_ProductProductPhoto_ProductID_ProductPhotoID")
                .IsClustered(false);

            entity.ToTable("ProductProductPhoto", "Production", tb => tb.HasComment("Cross-reference table mapping products and product photos."));

            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.ProductPhotoId)
                .HasComment("Product photo identification number. Foreign key to ProductPhoto.ProductPhotoID.")
                .HasColumnName("ProductPhotoID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductProductPhoto_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Primary).HasComment("0 = Photo is not the principal image. 1 = Photo is the principal image.");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductProductPhoto)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ProductPhoto).WithMany(p => p.ProductProductPhoto)
                .HasForeignKey(d => d.ProductPhotoId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ProductReviewId).HasName("PK_ProductReview_ProductReviewID");

            entity.ToTable("ProductReview", "Production", tb => tb.HasComment("Customer reviews of products they have purchased."));

            entity.HasIndex(e => new { e.ProductId, e.ReviewerName }, "IX_ProductReview_ProductID_Name");

            entity.Property(e => e.ProductReviewId)
                .HasComment("Primary key for ProductReview records.")
                .HasColumnName("ProductReviewID");
            entity.Property(e => e.Comments)
                .HasMaxLength(3850)
                .HasComment("Reviewer's comments");
            entity.Property(e => e.EmailAddress)
                .HasMaxLength(50)
                .HasComment("Reviewer's e-mail address.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductReview_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.Rating).HasComment("Product rating given by the reviewer. Scale is 1 to 5 with 5 as the highest rating.");
            entity.Property(e => e.ReviewDate)
                .HasComment("Date review was submitted.")
                .HasDefaultValueSql("(getdate())", "DF_ProductReview_ReviewDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ReviewerName)
                .HasMaxLength(50)
                .HasComment("Name of the reviewer.");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReview)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ProductSubcategory>(entity =>
        {
            entity.HasKey(e => e.ProductSubcategoryId).HasName("PK_ProductSubcategory_ProductSubcategoryID");

            entity.ToTable("ProductSubcategory", "Production", tb => tb.HasComment("Product subcategories. See ProductCategory table."));

            entity.HasIndex(e => e.Name, "AK_ProductSubcategory_Name").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_ProductSubcategory_rowguid").IsUnique();

            entity.Property(e => e.ProductSubcategoryId)
                .HasComment("Primary key for ProductSubcategory records.")
                .HasColumnName("ProductSubcategoryID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ProductSubcategory_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Subcategory description.");
            entity.Property(e => e.ProductCategoryId)
                .HasComment("Product category identification number. Foreign key to ProductCategory.ProductCategoryID.")
                .HasColumnName("ProductCategoryID");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_ProductSubcategory_rowguid")
                .HasColumnName("rowguid");

            entity.HasOne(d => d.ProductCategory).WithMany(p => p.ProductSubcategory)
                .HasForeignKey(d => d.ProductCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SalesOrderDetail>(entity =>
        {
            entity.HasKey(e => new { e.SalesOrderId, e.SalesOrderDetailId }).HasName("PK_SalesOrderDetail_SalesOrderID_SalesOrderDetailID");

            entity.ToTable("SalesOrderDetail", "Sales", tb =>
                {
                    tb.HasComment("Individual products associated with a specific sales order. See SalesOrderHeader.");
                    tb.HasTrigger("iduSalesOrderDetail");
                });

            entity.HasIndex(e => e.Rowguid, "AK_SalesOrderDetail_rowguid").IsUnique();

            entity.HasIndex(e => e.ProductId, "IX_SalesOrderDetail_ProductID");

            entity.Property(e => e.SalesOrderId)
                .HasComment("Primary key. Foreign key to SalesOrderHeader.SalesOrderID.")
                .HasColumnName("SalesOrderID");
            entity.Property(e => e.SalesOrderDetailId)
                .ValueGeneratedOnAdd()
                .HasComment("Primary key. One incremental unique number per product sold.")
                .HasColumnName("SalesOrderDetailID");
            entity.Property(e => e.CarrierTrackingNumber)
                .HasMaxLength(25)
                .HasComment("Shipment tracking number supplied by the shipper.");
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("(isnull(([UnitPrice]*((1.0)-[UnitPriceDiscount]))*[OrderQty],(0.0)))", false)
                .HasComment("Per product subtotal. Computed as UnitPrice * (1 - UnitPriceDiscount) * OrderQty.")
                .HasColumnType("numeric(38, 6)");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_SalesOrderDetail_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.OrderQty).HasComment("Quantity ordered per product.");
            entity.Property(e => e.ProductId)
                .HasComment("Product sold to customer. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_SalesOrderDetail_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.SpecialOfferId)
                .HasComment("Promotional code. Foreign key to SpecialOffer.SpecialOfferID.")
                .HasColumnName("SpecialOfferID");
            entity.Property(e => e.UnitPrice)
                .HasComment("Selling price of a single product.")
                .HasColumnType("money");
            entity.Property(e => e.UnitPriceDiscount)
                .HasComment("Discount amount.")
                .HasColumnType("money");

            entity.HasOne(d => d.SalesOrder).WithMany(p => p.SalesOrderDetail).HasForeignKey(d => d.SalesOrderId);
        });

        modelBuilder.Entity<SalesOrderHeader>(entity =>
        {
            entity.HasKey(e => e.SalesOrderId).HasName("PK_SalesOrderHeader_SalesOrderID");

            entity.ToTable("SalesOrderHeader", "Sales", tb =>
                {
                    tb.HasComment("General sales order information.");
                    tb.HasTrigger("uSalesOrderHeader");
                });

            entity.HasIndex(e => e.SalesOrderNumber, "AK_SalesOrderHeader_SalesOrderNumber").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_SalesOrderHeader_rowguid").IsUnique();

            entity.HasIndex(e => e.CustomerId, "IX_SalesOrderHeader_CustomerID");

            entity.HasIndex(e => e.SalesPersonId, "IX_SalesOrderHeader_SalesPersonID");

            entity.Property(e => e.SalesOrderId)
                .HasComment("Primary key.")
                .HasColumnName("SalesOrderID");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(15)
                .HasComment("Financial accounting number reference.");
            entity.Property(e => e.BillToAddressId)
                .HasComment("Customer billing address. Foreign key to Address.AddressID.")
                .HasColumnName("BillToAddressID");
            entity.Property(e => e.Comment)
                .HasMaxLength(128)
                .HasComment("Sales representative comments.");
            entity.Property(e => e.CreditCardApprovalCode)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasComment("Approval code provided by the credit card company.");
            entity.Property(e => e.CreditCardId)
                .HasComment("Credit card identification number. Foreign key to CreditCard.CreditCardID.")
                .HasColumnName("CreditCardID");
            entity.Property(e => e.CurrencyRateId)
                .HasComment("Currency exchange rate used. Foreign key to CurrencyRate.CurrencyRateID.")
                .HasColumnName("CurrencyRateID");
            entity.Property(e => e.CustomerId)
                .HasComment("Customer identification number. Foreign key to Customer.BusinessEntityID.")
                .HasColumnName("CustomerID");
            entity.Property(e => e.DueDate)
                .HasComment("Date the order is due to the customer.")
                .HasColumnType("datetime");
            entity.Property(e => e.Freight)
                .HasComment("Shipping cost.")
                .HasColumnType("money");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_SalesOrderHeader_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.OnlineOrderFlag)
                .HasComment("0 = Order placed by sales person. 1 = Order placed online by customer.")
                .HasDefaultValue(true, "DF_SalesOrderHeader_OnlineOrderFlag");
            entity.Property(e => e.OrderDate)
                .HasComment("Dates the sales order was created.")
                .HasDefaultValueSql("(getdate())", "DF_SalesOrderHeader_OrderDate")
                .HasColumnType("datetime");
            entity.Property(e => e.PurchaseOrderNumber)
                .HasMaxLength(25)
                .HasComment("Customer purchase order number reference. ");
            entity.Property(e => e.RevisionNumber).HasComment("Incremental number to track changes to the sales order over time.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_SalesOrderHeader_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.SalesOrderNumber)
                .HasMaxLength(25)
                .HasComputedColumnSql("(isnull(N'SO'+CONVERT([nvarchar](23),[SalesOrderID]),N'*** ERROR ***'))", false)
                .HasComment("Unique sales order identification number.");
            entity.Property(e => e.SalesPersonId)
                .HasComment("Sales person who created the sales order. Foreign key to SalesPerson.BusinessEntityID.")
                .HasColumnName("SalesPersonID");
            entity.Property(e => e.ShipDate)
                .HasComment("Date the order was shipped to the customer.")
                .HasColumnType("datetime");
            entity.Property(e => e.ShipMethodId)
                .HasComment("Shipping method. Foreign key to ShipMethod.ShipMethodID.")
                .HasColumnName("ShipMethodID");
            entity.Property(e => e.ShipToAddressId)
                .HasComment("Customer shipping address. Foreign key to Address.AddressID.")
                .HasColumnName("ShipToAddressID");
            entity.Property(e => e.Status)
                .HasComment("Order current status. 1 = In process; 2 = Approved; 3 = Backordered; 4 = Rejected; 5 = Shipped; 6 = Cancelled")
                .HasDefaultValue((byte)1, "DF_SalesOrderHeader_Status");
            entity.Property(e => e.SubTotal)
                .HasComment("Sales subtotal. Computed as SUM(SalesOrderDetail.LineTotal)for the appropriate SalesOrderID.")
                .HasColumnType("money");
            entity.Property(e => e.TaxAmt)
                .HasComment("Tax amount.")
                .HasColumnType("money");
            entity.Property(e => e.TerritoryId)
                .HasComment("Territory in which the sale was made. Foreign key to SalesTerritory.SalesTerritoryID.")
                .HasColumnName("TerritoryID");
            entity.Property(e => e.TotalDue)
                .HasComputedColumnSql("(isnull(([SubTotal]+[TaxAmt])+[Freight],(0)))", false)
                .HasComment("Total due from customer. Computed as Subtotal + TaxAmt + Freight.")
                .HasColumnType("money");

            entity.HasOne(d => d.BillToAddress).WithMany(p => p.SalesOrderHeaderBillToAddress)
                .HasForeignKey(d => d.BillToAddressId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Customer).WithMany(p => p.SalesOrderHeader)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ShipToAddress).WithMany(p => p.SalesOrderHeaderShipToAddress)
                .HasForeignKey(d => d.ShipToAddressId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ScrapReason>(entity =>
        {
            entity.HasKey(e => e.ScrapReasonId).HasName("PK_ScrapReason_ScrapReasonID");

            entity.ToTable("ScrapReason", "Production", tb => tb.HasComment("Manufacturing failure reasons lookup table."));

            entity.HasIndex(e => e.Name, "AK_ScrapReason_Name").IsUnique();

            entity.Property(e => e.ScrapReasonId)
                .HasComment("Primary key for ScrapReason records.")
                .HasColumnName("ScrapReasonID");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_ScrapReason_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Failure description.");
        });

        modelBuilder.Entity<StateProvince>(entity =>
        {
            entity.HasKey(e => e.StateProvinceId).HasName("PK_StateProvince_StateProvinceID");

            entity.ToTable("StateProvince", "Person", tb => tb.HasComment("State and province lookup table."));

            entity.HasIndex(e => e.Name, "AK_StateProvince_Name").IsUnique();

            entity.HasIndex(e => new { e.StateProvinceCode, e.CountryRegionCode }, "AK_StateProvince_StateProvinceCode_CountryRegionCode").IsUnique();

            entity.HasIndex(e => e.Rowguid, "AK_StateProvince_rowguid").IsUnique();

            entity.Property(e => e.StateProvinceId)
                .HasComment("Primary key for StateProvince records.")
                .HasColumnName("StateProvinceID");
            entity.Property(e => e.CountryRegionCode)
                .HasMaxLength(3)
                .HasComment("ISO standard country or region code. Foreign key to CountryRegion.CountryRegionCode. ");
            entity.Property(e => e.IsOnlyStateProvinceFlag)
                .HasComment("0 = StateProvinceCode exists. 1 = StateProvinceCode unavailable, using CountryRegionCode.")
                .HasDefaultValue(true, "DF_StateProvince_IsOnlyStateProvinceFlag");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_StateProvince_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("State or province description.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_StateProvince_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.StateProvinceCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasComment("ISO standard state or province code.");
            entity.Property(e => e.TerritoryId)
                .HasComment("ID of the territory in which the state or province is located. Foreign key to SalesTerritory.SalesTerritoryID.")
                .HasColumnName("TerritoryID");

            entity.HasOne(d => d.CountryRegionCodeNavigation).WithMany(p => p.StateProvince)
                .HasForeignKey(d => d.CountryRegionCode)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.BusinessEntityId).HasName("PK_Store_BusinessEntityID");

            entity.ToTable("Store", "Sales", tb => tb.HasComment("Customers (resellers) of Adventure Works products."));

            entity.HasIndex(e => e.Rowguid, "AK_Store_rowguid").IsUnique();

            entity.HasIndex(e => e.SalesPersonId, "IX_Store_SalesPersonID");

            entity.HasIndex(e => e.Demographics, "PXML_Store_Demographics");

            entity.Property(e => e.BusinessEntityId)
                .ValueGeneratedNever()
                .HasComment("Primary key. Foreign key to Customer.BusinessEntityID.")
                .HasColumnName("BusinessEntityID");
            entity.Property(e => e.Demographics)
                .HasComment("Demographic informationg about the store such as the number of employees, annual sales and store type.")
                .HasColumnType("xml");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_Store_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Name of the store.");
            entity.Property(e => e.Rowguid)
                .HasComment("ROWGUIDCOL number uniquely identifying the record. Used to support a merge replication sample.")
                .HasDefaultValueSql("(newid())", "DF_Store_rowguid")
                .HasColumnName("rowguid");
            entity.Property(e => e.SalesPersonId)
                .HasComment("ID of the sales person assigned to the customer. Foreign key to SalesPerson.BusinessEntityID.")
                .HasColumnName("SalesPersonID");

            entity.HasOne(d => d.BusinessEntity).WithOne(p => p.Store)
                .HasForeignKey<Store>(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TransactionHistory>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK_TransactionHistory_TransactionID");

            entity.ToTable("TransactionHistory", "Production", tb => tb.HasComment("Record of each purchase order, sales order, or work order transaction year to date."));

            entity.HasIndex(e => e.ProductId, "IX_TransactionHistory_ProductID");

            entity.HasIndex(e => new { e.ReferenceOrderId, e.ReferenceOrderLineId }, "IX_TransactionHistory_ReferenceOrderID_ReferenceOrderLineID");

            entity.Property(e => e.TransactionId)
                .HasComment("Primary key for TransactionHistory records.")
                .HasColumnName("TransactionID");
            entity.Property(e => e.ActualCost)
                .HasComment("Product cost.")
                .HasColumnType("money");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_TransactionHistory_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.Quantity).HasComment("Product quantity.");
            entity.Property(e => e.ReferenceOrderId)
                .HasComment("Purchase order, sales order, or work order identification number.")
                .HasColumnName("ReferenceOrderID");
            entity.Property(e => e.ReferenceOrderLineId)
                .HasComment("Line number associated with the purchase order, sales order, or work order.")
                .HasColumnName("ReferenceOrderLineID");
            entity.Property(e => e.TransactionDate)
                .HasComment("Date and time of the transaction.")
                .HasDefaultValueSql("(getdate())", "DF_TransactionHistory_TransactionDate")
                .HasColumnType("datetime");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("W = WorkOrder, S = SalesOrder, P = PurchaseOrder");

            entity.HasOne(d => d.Product).WithMany(p => p.TransactionHistory)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TransactionHistoryArchive>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK_TransactionHistoryArchive_TransactionID");

            entity.ToTable("TransactionHistoryArchive", "Production", tb => tb.HasComment("Transactions for previous years."));

            entity.HasIndex(e => e.ProductId, "IX_TransactionHistoryArchive_ProductID");

            entity.HasIndex(e => new { e.ReferenceOrderId, e.ReferenceOrderLineId }, "IX_TransactionHistoryArchive_ReferenceOrderID_ReferenceOrderLineID");

            entity.Property(e => e.TransactionId)
                .ValueGeneratedNever()
                .HasComment("Primary key for TransactionHistoryArchive records.")
                .HasColumnName("TransactionID");
            entity.Property(e => e.ActualCost)
                .HasComment("Product cost.")
                .HasColumnType("money");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_TransactionHistoryArchive_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ProductId)
                .HasComment("Product identification number. Foreign key to Product.ProductID.")
                .HasColumnName("ProductID");
            entity.Property(e => e.Quantity).HasComment("Product quantity.");
            entity.Property(e => e.ReferenceOrderId)
                .HasComment("Purchase order, sales order, or work order identification number.")
                .HasColumnName("ReferenceOrderID");
            entity.Property(e => e.ReferenceOrderLineId)
                .HasComment("Line number associated with the purchase order, sales order, or work order.")
                .HasColumnName("ReferenceOrderLineID");
            entity.Property(e => e.TransactionDate)
                .HasComment("Date and time of the transaction.")
                .HasDefaultValueSql("(getdate())", "DF_TransactionHistoryArchive_TransactionDate")
                .HasColumnType("datetime");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(1)
                .IsFixedLength()
                .HasComment("W = Work Order, S = Sales Order, P = Purchase Order");
        });

        modelBuilder.Entity<UnitMeasure>(entity =>
        {
            entity.HasKey(e => e.UnitMeasureCode).HasName("PK_UnitMeasure_UnitMeasureCode");

            entity.ToTable("UnitMeasure", "Production", tb => tb.HasComment("Unit of measure lookup table."));

            entity.HasIndex(e => e.Name, "AK_UnitMeasure_Name").IsUnique();

            entity.Property(e => e.UnitMeasureCode)
                .HasMaxLength(3)
                .IsFixedLength()
                .HasComment("Primary key.");
            entity.Property(e => e.ModifiedDate)
                .HasComment("Date and time the record was last updated.")
                .HasDefaultValueSql("(getdate())", "DF_UnitMeasure_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasComment("Unit of measure description.");
        });

        modelBuilder.Entity<VProductAndDescription>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vProductAndDescription", "Production");

            entity.Property(e => e.CultureId)
                .HasMaxLength(6)
                .IsFixedLength()
                .HasColumnName("CultureID");
            entity.Property(e => e.Description).HasMaxLength(400);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ProductModel).HasMaxLength(50);
        });

        modelBuilder.Entity<VProductModelCatalogDescription>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vProductModelCatalogDescription", "Production");

            entity.Property(e => e.Color).HasMaxLength(256);
            entity.Property(e => e.Copyright).HasMaxLength(30);
            entity.Property(e => e.Crankset).HasMaxLength(256);
            entity.Property(e => e.MaintenanceDescription).HasMaxLength(256);
            entity.Property(e => e.Material).HasMaxLength(256);
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.NoOfYears).HasMaxLength(256);
            entity.Property(e => e.Pedal).HasMaxLength(256);
            entity.Property(e => e.PictureAngle).HasMaxLength(256);
            entity.Property(e => e.PictureSize).HasMaxLength(256);
            entity.Property(e => e.ProductLine).HasMaxLength(256);
            entity.Property(e => e.ProductModelId)
                .ValueGeneratedOnAdd()
                .HasColumnName("ProductModelID");
            entity.Property(e => e.ProductPhotoId)
                .HasMaxLength(256)
                .HasColumnName("ProductPhotoID");
            entity.Property(e => e.ProductUrl)
                .HasMaxLength(256)
                .HasColumnName("ProductURL");
            entity.Property(e => e.RiderExperience).HasMaxLength(1024);
            entity.Property(e => e.Rowguid).HasColumnName("rowguid");
            entity.Property(e => e.Saddle).HasMaxLength(256);
            entity.Property(e => e.Style).HasMaxLength(256);
            entity.Property(e => e.WarrantyDescription).HasMaxLength(256);
            entity.Property(e => e.WarrantyPeriod).HasMaxLength(256);
            entity.Property(e => e.Wheel).HasMaxLength(256);
        });

        modelBuilder.Entity<VProductModelInstructions>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vProductModelInstructions", "Production");

            entity.Property(e => e.LaborHours).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.MachineHours).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.ModifiedDate).HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ProductModelId)
                .ValueGeneratedOnAdd()
                .HasColumnName("ProductModelID");
            entity.Property(e => e.Rowguid).HasColumnName("rowguid");
            entity.Property(e => e.SetupHours).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.Step).HasMaxLength(1024);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.HasKey(e => e.WorkOrderId).HasName("PK_WorkOrder_WorkOrderID");

            entity.ToTable("WorkOrder", "Production", tb =>
                {
                    tb.HasTrigger("iWorkOrder");
                    tb.HasTrigger("uWorkOrder");
                });

            entity.HasIndex(e => e.ProductId, "IX_WorkOrder_ProductID");

            entity.HasIndex(e => e.ScrapReasonId, "IX_WorkOrder_ScrapReasonID");

            entity.Property(e => e.WorkOrderId).HasColumnName("WorkOrderID");
            entity.Property(e => e.DueDate).HasColumnType("datetime");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("(getdate())", "DF_WorkOrder_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ScrapReasonId).HasColumnName("ScrapReasonID");
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.StockedQty).HasComputedColumnSql("(isnull([OrderQty]-[ScrappedQty],(0)))", false);

            entity.HasOne(d => d.Product).WithMany(p => p.WorkOrder)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ScrapReason).WithMany(p => p.WorkOrder).HasForeignKey(d => d.ScrapReasonId);
        });

        modelBuilder.Entity<WorkOrderRouting>(entity =>
        {
            entity.HasKey(e => new { e.WorkOrderId, e.ProductId, e.OperationSequence }).HasName("PK_WorkOrderRouting_WorkOrderID_ProductID_OperationSequence");

            entity.ToTable("WorkOrderRouting", "Production");

            entity.HasIndex(e => e.ProductId, "IX_WorkOrderRouting_ProductID");

            entity.Property(e => e.WorkOrderId).HasColumnName("WorkOrderID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ActualCost).HasColumnType("money");
            entity.Property(e => e.ActualEndDate).HasColumnType("datetime");
            entity.Property(e => e.ActualResourceHrs).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.ActualStartDate).HasColumnType("datetime");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.ModifiedDate)
                .HasDefaultValueSql("(getdate())", "DF_WorkOrderRouting_ModifiedDate")
                .HasColumnType("datetime");
            entity.Property(e => e.PlannedCost).HasColumnType("money");
            entity.Property(e => e.ScheduledEndDate).HasColumnType("datetime");
            entity.Property(e => e.ScheduledStartDate).HasColumnType("datetime");

            entity.HasOne(d => d.Location).WithMany(p => p.WorkOrderRouting)
                .HasForeignKey(d => d.LocationId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.WorkOrder).WithMany(p => p.WorkOrderRouting)
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
