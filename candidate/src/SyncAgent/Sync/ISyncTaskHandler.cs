namespace SyncAgent.Sync;

/// <summary>
/// Executes one kind of sync task.
/// </summary>
/// <remarks>
/// This is the extension point. Supporting a fifth task type means writing one class that
/// implements this interface and adding one registration line in <c>Program.cs</c>; the
/// dispatcher, the worker and the platform client are untouched.
/// </remarks>
public interface ISyncTaskHandler
{
    /// <summary>
    /// The <c>taskType</c> this handler answers to, matched case-insensitively.
    /// The set of handler task types <em>is</em> the allowlist -- there is no second list to
    /// keep in sync.
    /// </summary>
    string TaskType { get; }

    /// <summary>Runs the query and returns the rows to be posted back, already shaped for the contract.</summary>
    Task<IReadOnlyList<object>> ExecuteAsync(SyncParameters parameters, CancellationToken cancellationToken);
}
