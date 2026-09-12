using Microsoft.Data.SqlClient;

namespace SyncAgent.Infrastructure.Sql;

internal static class SqlDataReaderExtensions
{
    /// <summary>
    /// Reads a `datetime` column as UTC. SQL Server's datetime carries no offset, so the value comes
    /// back as Unspecified and would serialize without the trailing "Z" the contract's timestamps use.
    /// AdventureWorks timestamps are treated as UTC throughout, which is also how the ModifiedSince
    /// filter compares against these columns.
    /// </summary>
    public static DateTime GetUtcDateTime(this SqlDataReader reader, int ordinal) =>
        DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
}
