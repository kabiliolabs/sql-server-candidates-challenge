using System.Data.SqlClient;

namespace SyncAgent.Sql
{
    public static class SqlReaderExtensions
    {
        public static string GetStringOrNull(this SqlDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}