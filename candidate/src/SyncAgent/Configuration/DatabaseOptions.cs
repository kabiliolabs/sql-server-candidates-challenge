using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
