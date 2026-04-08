
namespace SyncPlatformClient.Domain
{
    public static class DateTimeHelper
    {
        public static DateTime? GetModifiedDate(string modifiedSinceDate)
        {
            if (DateTime.TryParse(modifiedSinceDate, out var date))
                return date.ToUniversalTime();

            return null;
        }
    }
}
