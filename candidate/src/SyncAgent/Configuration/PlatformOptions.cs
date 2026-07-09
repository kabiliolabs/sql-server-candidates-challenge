using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Configuration;

/// <summary>
/// Connection settings for the central platform API.
/// <para>
/// <see cref="ApiKey"/> is a secret. It is bound from configuration (environment variable or
/// user-secrets in development) and must never be committed to source or written to a log.
/// </para>
/// </summary>
public sealed class PlatformOptions : IValidatableObject
{
    public const string SectionName = "Platform";

    /// <summary>
    /// The single origin this agent will ever talk to. The agent never follows a
    /// server-supplied URL, so a compromised platform cannot redirect it elsewhere.
    /// </summary>
    /// <remarks>
    /// Deliberately not marked <c>[Required]</c>: the property has a non-null default, so such an
    /// annotation could never fail. The invariant that actually matters -- an absolute http(s)
    /// origin -- is enforced in <see cref="Validate"/>, so a <c>file://</c> or relative URI is
    /// rejected at startup rather than pointing the agent somewhere it should never go.
    /// </remarks>
    public Uri BaseAddress { get; init; } = new("http://localhost:5100");

    [Required(AllowEmptyStrings = false, ErrorMessage = "Platform:ApiKey must be supplied (environment variable or user-secrets).")]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Ceiling on a single HTTP request, including the POST of a large result body.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(100);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!BaseAddress.IsAbsoluteUri)
        {
            yield return new ValidationResult(
                "Platform:BaseAddress must be an absolute URI.",
                [nameof(BaseAddress)]);

            // Uri.Scheme throws on a relative URI, so there is nothing further to check.
            yield break;
        }

        if (BaseAddress.Scheme != Uri.UriSchemeHttp && BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult(
                "Platform:BaseAddress must use the http or https scheme.",
                [nameof(BaseAddress)]);
        }
    }
}
