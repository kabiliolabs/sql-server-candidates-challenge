using System;
using System.Collections.Generic;

namespace SyncAgent.Infrastructure.Platform
{
    /// <summary>
    /// Wire shape of <c>GET /api/sync/next-task</c>.
    /// </summary>
    /// <remarks>
    /// A separate type from <see cref="Core.Models.SyncTaskRequest"/> on purpose. This one
    /// models what the network can produce — every field nullable, nothing guaranteed — while
    /// the domain type models what the agent is willing to work with. The conversion between
    /// them is where validation happens, and having two types makes it impossible to skip.
    /// </remarks>
    internal sealed class SyncTaskDto
    {
        public string? TaskId { get; set; }

        public string? TaskType { get; set; }

        public Dictionary<string, string>? Parameters { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
