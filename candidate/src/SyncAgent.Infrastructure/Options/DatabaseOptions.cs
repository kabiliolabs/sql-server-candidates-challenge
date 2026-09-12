namespace SyncAgent.Infrastructure.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Server=localhost\\SQLEXPRESS;Database=AdventureWorks2025;Trusted_Connection=True;TrustServerCertificate=True;";
    public int CommandTimeoutSeconds { get; set; } = 30;
}
