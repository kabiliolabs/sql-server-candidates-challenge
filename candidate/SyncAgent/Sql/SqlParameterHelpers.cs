using System;
using System.Data;
using System.Data.SqlClient;

namespace SyncAgent.Sql
{
    /// <summary>Shared SQL parameter builders. Parameterized queries only — never string concatenation.</summary>
    public static class SqlParameterHelpers
    {
        public static SqlParameter ModifiedSince(DateTime? modifiedSince)
        {
            return new SqlParameter("@ModifiedSince", SqlDbType.DateTime2)
            {
                Value = (object)modifiedSince ?? DBNull.Value
            };
        }
    }
}