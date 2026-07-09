using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Configuration;

/// <summary>
/// Connection settings for the local client database.
/// Bound from <c>ConnectionStrings:AdventureWorks</c> so it reads naturally in configuration files.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(AllowEmptyStrings = false, ErrorMessage = "ConnectionStrings:AdventureWorks must be supplied.")]
    public string AdventureWorks { get; init; } = string.Empty;
}
