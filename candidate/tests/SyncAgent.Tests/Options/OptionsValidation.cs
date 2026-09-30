using System.ComponentModel.DataAnnotations;

namespace SyncAgent.Tests.Options;

/// <summary>Runs the same Data Annotations + IValidatableObject validation that ValidateDataAnnotations() uses.</summary>
internal static class OptionsValidation
{
    public static IReadOnlyList<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
