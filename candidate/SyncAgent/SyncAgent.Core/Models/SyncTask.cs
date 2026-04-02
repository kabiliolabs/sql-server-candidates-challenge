using System.Globalization;

namespace SyncAgent.Core.Models;

public class SyncTask
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    public DateTime? GetModifiedSince()
    {
        return TryGetModifiedSince(out var modifiedSince) ? modifiedSince : null;
    }

    public bool TryGetModifiedSince(out DateTime? modifiedSince)
    {
        modifiedSince = null;

        if (!Parameters.TryGetValue("modifiedSince", out var value) || value is null)
            return true;

        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return true;

        if (DateTime.TryParse(
            str,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            modifiedSince = parsed;
            return true;
        }

        return false;
    }
}
