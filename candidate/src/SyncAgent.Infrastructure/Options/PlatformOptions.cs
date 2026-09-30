using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Infrastructure.Options
{
    public sealed class PlatformOptions : IValidatableObject
    {
        public const string SectionName = "Platform";

        [Required, Url]
        public string BaseUrl { get; init; } = string.Empty;

        [Required, MinLength(8)]
        public string ApiKey { get; init; } = string.Empty;

        [Range(1, 3600)]
        public int PollingIntervalSeconds { get; init; } = 5;

        [Range(1, 300)]
        public int RequestTimeoutSeconds { get; init; } = 30;

        [Range(1, 3600)]
        public int MaxBackoffSeconds { get; init; } = 60;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
            {
                // Security: the API key travels in a header, so plain HTTP is only allowed against localhost
                if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
                    yield return new ValidationResult(
                        "BaseUrl must use HTTPS for non-local hosts.", [nameof(BaseUrl)]);
            }

            // Backoff starts at the polling interval and grows up to this cap
            if (MaxBackoffSeconds < PollingIntervalSeconds)
                yield return new ValidationResult(
                    "MaxBackoffSeconds must be greater than or equal to PollingIntervalSeconds.",
                    [nameof(MaxBackoffSeconds)]);
        }
    }
}