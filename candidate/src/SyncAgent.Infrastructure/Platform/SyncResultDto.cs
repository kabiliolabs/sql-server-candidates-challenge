using System;
using System.Collections.Generic;
using SyncAgent.Core.Models;

namespace SyncAgent.Infrastructure.Platform
{
    /// <summary>
    /// Wire shape of the <c>POST /api/sync/result</c> body.
    /// </summary>
    /// <remarks>
    /// <see cref="ExecutedAt"/> is a <see cref="DateTime"/> rather than the domain's
    /// <see cref="DateTimeOffset"/>: serialised as UTC it renders with the trailing Z the
    /// contract shows, where a DateTimeOffset would render "+00:00".
    /// </remarks>
    internal sealed class SyncResultDto
    {
        public string TaskId { get; set; } = string.Empty;

        public string TaskType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public IReadOnlyList<object>? Data { get; set; }

        public int RecordCount { get; set; }

        public DateTime ExecutedAt { get; set; }

        public string? ErrorMessage { get; set; }

        public static SyncResultDto FromDomain(SyncTaskResult result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return new SyncResultDto
            {
                TaskId = result.TaskId,
                TaskType = result.TaskType,
                Status = result.Status,
                Data = result.Data,
                RecordCount = result.RecordCount,
                ExecutedAt = result.ExecutedAt.UtcDateTime,
                ErrorMessage = result.ErrorMessage,
            };
        }
    }
}
