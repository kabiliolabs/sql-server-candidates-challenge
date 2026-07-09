using System.Globalization;
using SyncAgent.Platform.Contracts;

namespace SyncAgent.Validation;

/// <summary>
/// The agent's trust boundary. Everything arriving from the platform is untrusted until it has
/// passed through here.
/// </summary>
/// <remarks>
/// <para>
/// <c>modifiedSince</c> is the only externally-supplied value that reaches SQL Server. It is
/// always bound as a <c>DbParameter</c> and never concatenated into a statement, so parameter
/// binding alone already defeats injection. This validator is the second layer: the value must
/// parse as a strict ISO-8601 instant <em>with an explicit offset</em>, or the task never runs.
/// </para>
/// <para>
/// The parse is deliberately <see cref="DateTimeOffset.TryParseExact(string, string[], IFormatProvider, DateTimeStyles, out DateTimeOffset)"/>
/// against a closed list of formats rather than the permissive <c>TryParse</c>. <c>TryParse</c>
/// accepts a wide, culture-sensitive surface -- "January 1" or "1/1/2025" -- which is a poor fit
/// for a machine-to-machine contract that documents ISO-8601. A narrow accepted surface is easier
/// to reason about than a broad one.
/// </para>
/// </remarks>
public sealed class TaskValidator : ITaskValidator
{
    internal const string ModifiedSinceParameterName = "modifiedSince";

    /// <summary>
    /// ISO-8601 with a required offset, either the literal 'Z' or a numeric offset such as +02:00.
    /// <c>FFFFFFF</c> makes fractional seconds optional, which covers both "…T00:00:00Z" and
    /// "…T10:01:36.827Z".
    /// </summary>
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-ddTHH:mm:ss'Z'",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
    ];

    private const DateTimeStyles ParseStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public TaskValidationResult Validate(SyncTask? task)
    {
        if (task is null)
        {
            return TaskValidationResult.Unaddressable("Task payload was null.");
        }

        // Without both of these the platform answers 400 to any result we post, so there is no
        // failure we could usefully report. Bail out before touching anything else.
        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            return TaskValidationResult.Unaddressable("Task is missing required field: taskId.");
        }

        if (string.IsNullOrWhiteSpace(task.TaskType))
        {
            return TaskValidationResult.Unaddressable("Task is missing required field: taskType.");
        }

        if (task.Parameters is null || !task.Parameters.TryGetValue(ModifiedSinceParameterName, out var rawModifiedSince))
        {
            return TaskValidationResult.Rejected($"Task is missing required parameter: {ModifiedSinceParameterName}.");
        }

        if (!DateTimeOffset.TryParseExact(rawModifiedSince, AcceptedFormats, CultureInfo.InvariantCulture, ParseStyles, out var modifiedSince))
        {
            // The offending value is deliberately not echoed back: it is attacker-controlled and
            // would otherwise be reflected into the platform's log viewer.
            return TaskValidationResult.Rejected(
                $"Parameter '{ModifiedSinceParameterName}' is not a valid ISO-8601 instant with an explicit UTC offset.");
        }

        return TaskValidationResult.Valid(modifiedSince);
    }
}
