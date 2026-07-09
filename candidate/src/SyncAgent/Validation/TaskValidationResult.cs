namespace SyncAgent.Validation;

/// <summary>
/// Why a task was or wasn't accepted. Three states, because "invalid" splits in two:
/// a task we can reject <em>and report</em>, versus one we cannot even address.
/// </summary>
public enum TaskValidationOutcome
{
    /// <summary>The task is well-formed and safe to execute.</summary>
    Valid,

    /// <summary>Identifiable but unusable. The agent can and should POST a "failed" result.</summary>
    Rejected,

    /// <summary>
    /// Missing <c>taskId</c> or <c>taskType</c>. There is nothing to report a failure
    /// <em>against</em> -- the platform answers 400 to a result with either field missing --
    /// so the agent can only log and move on.
    /// </summary>
    Unaddressable,
}

/// <summary>The outcome of validating one <see cref="Platform.Contracts.SyncTask"/>.</summary>
public sealed record TaskValidationResult
{
    private TaskValidationResult()
    {
    }

    public TaskValidationOutcome Outcome { get; private init; }

    /// <summary>Populated only when <see cref="Outcome"/> is <see cref="TaskValidationOutcome.Valid"/>.</summary>
    public DateTimeOffset ModifiedSince { get; private init; }

    /// <summary>A message safe to send to the platform. Never populated for a valid task.</summary>
    public string? Error { get; private init; }

    public bool IsValid => Outcome == TaskValidationOutcome.Valid;

    public static TaskValidationResult Valid(DateTimeOffset modifiedSince) =>
        new() { Outcome = TaskValidationOutcome.Valid, ModifiedSince = modifiedSince };

    public static TaskValidationResult Rejected(string error) =>
        new() { Outcome = TaskValidationOutcome.Rejected, Error = error };

    public static TaskValidationResult Unaddressable(string error) =>
        new() { Outcome = TaskValidationOutcome.Unaddressable, Error = error };
}
