using System.Collections.Generic;
using System.Linq;

namespace SyncAgent.Core.Models
{
    /// <summary>
    /// What a handler produces: the records to ship, plus whether the row limit clipped them.
    /// </summary>
    /// <remarks>
    /// The API contract has no field for truncation, so the flag never leaves the process.
    /// It exists so the agent can log loudly when a sync returned a partial view of the data,
    /// which is otherwise indistinguishable from a genuinely small result set.
    /// </remarks>
    public sealed class SyncPayload
    {
        public SyncPayload(IReadOnlyList<object> records, bool wasTruncated)
        {
            Records = records;
            WasTruncated = wasTruncated;
        }

        public static SyncPayload Empty { get; } = new SyncPayload(new object[0], wasTruncated: false);

        public IReadOnlyList<object> Records { get; }

        public bool WasTruncated { get; }

        /// <summary>
        /// Wraps query output, flagging truncation when the row count reached the configured
        /// ceiling. Equality is the correct test because the queries ask for at most
        /// <paramref name="maxRecords"/> rows: coming back with exactly that many means more
        /// rows were available behind the limit.
        /// </summary>
        public static SyncPayload From<T>(IReadOnlyList<T> records, int maxRecords)
            where T : class
        {
            return new SyncPayload(
                records.Cast<object>().ToList(),
                wasTruncated: records.Count >= maxRecords);
        }
    }
}
