using System;
using System.Data;

namespace SyncAgent.Infrastructure.Data
{
    /// <summary>
    /// Reader helpers for the two cases ADO.NET does not cover well: nullable columns and
    /// <c>datetime</c> values that need a kind attached.
    /// </summary>
    internal static class DataRecordExtensions
    {
        /// <summary>
        /// Reads a nullable string column, mapping <c>NULL</c> to <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="IDataRecord.GetString"/> throws on <c>NULL</c>, and several columns in
        /// these projections are legitimately optional: a product with no subcategory, a
        /// customer with no e-mail on file.
        /// </remarks>
        public static string? GetNullableString(this IDataRecord record, int ordinal)
        {
            return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
        }

        /// <summary>
        /// Reads a <c>datetime</c> column and tags it as UTC.
        /// </summary>
        /// <remarks>
        /// AdventureWorks stores <c>datetime</c>, which carries no zone, so ADO.NET returns
        /// <see cref="DateTimeKind.Unspecified"/>. Serialising that produces a timestamp with
        /// no zone marker, while the contract shows every timestamp ending in Z. The stored
        /// values are therefore taken to already be UTC and labelled as such — no conversion
        /// is applied, because shifting them by the agent machine's local offset would
        /// silently move every record by an amount that depends on where the client is.
        /// </remarks>
        public static DateTime GetUtcDateTime(this IDataRecord record, int ordinal)
        {
            return DateTime.SpecifyKind(record.GetDateTime(ordinal), DateTimeKind.Utc);
        }
    }
}
