using System.ComponentModel.DataAnnotations;

using Microsoft.Data.SqlClient;

namespace SyncAgent.Infrastructure.Options
{
    public sealed class DatabaseOptions : IValidatableObject
    {
        public const string ConnectionStringName = "AdventureWorks";

        [Required(ErrorMessage = "Missing connection string 'ConnectionStrings:AdventureWorks'. " +
                                 "Set it with dotnet user-secrets or the ConnectionStrings__AdventureWorks environment variable.")]
        public string ConnectionString { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                yield break;

            SqlConnectionStringBuilder? builder = null;
            ValidationResult? formatError = null;
            try
            {
                builder = new SqlConnectionStringBuilder(ConnectionString);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
            {
                formatError = new ValidationResult(
                    "Connection string 'AdventureWorks' has an invalid format.", [nameof(ConnectionString)]);
            }

            if (formatError is not null)
            {
                yield return formatError;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(builder!.DataSource))
                yield return new ValidationResult(
                    "Connection string must specify a server (Server / Data Source).", [nameof(ConnectionString)]);

            // Without an explicit database the login's default (usually master) would be used
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                yield return new ValidationResult(
                    "Connection string must specify a database (Database / Initial Catalog).", [nameof(ConnectionString)]);
        }
    }
}