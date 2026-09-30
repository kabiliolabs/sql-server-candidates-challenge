namespace SyncAgent.Core.Validation
{
    public static class LogSanitizer
    {
        private const int MaxLength = 64;

        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "(empty)";

            var clean = new string(value.Where(c => !char.IsControl(c)).Take(MaxLength).ToArray());
            return value.Length > MaxLength ? clean + "…" : clean;
        }
    }
}