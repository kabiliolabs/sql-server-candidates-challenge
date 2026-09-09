namespace SyncAgent.Core.Models
{
    /// <summary>
    /// The two status values the platform accepts on <c>POST /api/sync/result</c>.
    /// </summary>
    /// <remarks>
    /// These are deliberately constants rather than an enum. The platform compares the
    /// incoming value with ordinal, case-sensitive equality against these exact lowercase
    /// literals, so an enum plus a serializer naming convention would insert a translation
    /// step between us and a contract we do not control.
    /// </remarks>
    public static class SyncTaskStatus
    {
        public const string Completed = "completed";

        public const string Failed = "failed";
    }
}
