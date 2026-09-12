namespace SyncAgent.Domain;

/// <summary>
/// Raised when a task's parameters are missing or malformed. Reported back to the platform as a
/// failed task rather than treated as an agent fault.
/// </summary>
public sealed class SyncTaskValidationException(string message) : Exception(message);
