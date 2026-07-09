using System.Data.Common;

namespace SyncAgent.Sync;

/// <summary>
/// Turns an exception into a message that is safe to send across the network.
/// </summary>
/// <remarks>
/// Raw exception text is hostile to share. A <see cref="DbException"/> message routinely names the
/// server, instance and database; a stack trace maps the agent's internals. The platform's log
/// viewer renders whatever it is given, so an unsanitised message is an information-disclosure
/// channel that also happens to be an attacker-visible oracle.
/// <para>
/// Matching on <see cref="DbException"/> rather than on <c>SqlException</c> keeps this file, and
/// with it the whole failure boundary, independent of which ADO.NET provider the agent is built
/// against.
/// </para>
/// <para>
/// The full exception is still written to the <em>local</em> log, where the operator who owns the
/// machine can already see everything anyway. Only the category crosses the wire.
/// </para>
/// </remarks>
internal static class ErrorSanitizer
{
    public static string Sanitize(Exception exception) => exception switch
    {
        DbException => "Database query failed on the client machine. See the agent log for details.",
        TimeoutException => "Database query timed out on the client machine.",
        InvalidOperationException => "Agent could not execute the task. See the agent log for details.",
        _ => "Task execution failed on the client machine. See the agent log for details.",
    };
}
