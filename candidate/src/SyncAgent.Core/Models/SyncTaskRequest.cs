using System;
using System.Collections.Generic;

namespace SyncAgent.Core.Models
{
    /// <summary>
    /// A unit of work handed to the agent by <c>GET /api/sync/next-task</c>.
    /// </summary>
    /// <remarks>
    /// An immutable carrier for data that arrived over the network, so it stays untrusted
    /// until the validator has passed it. Keeping the rules out of the constructor is
    /// deliberate: they depend on which handlers are registered and on configured bounds,
    /// and neither belongs inside a DTO.
    /// </remarks>
    public sealed class SyncTaskRequest
    {
        private static readonly IReadOnlyDictionary<string, string> NoParameters =
            new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

        public SyncTaskRequest(
            string? taskId,
            string? taskType,
            IReadOnlyDictionary<string, string>? parameters,
            DateTimeOffset createdAt)
        {
            TaskId = taskId ?? string.Empty;
            TaskType = taskType ?? string.Empty;
            Parameters = parameters ?? NoParameters;
            CreatedAt = createdAt;
        }

        /// <summary>ULID assigned by the platform. Echoed back verbatim on the result.</summary>
        public string TaskId { get; }

        /// <summary>Selects the handler, for example <c>GetCustomers</c>.</summary>
        public string TaskType { get; }

        /// <summary>Task-specific filters. The current contract only defines <c>modifiedSince</c>.</summary>
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
