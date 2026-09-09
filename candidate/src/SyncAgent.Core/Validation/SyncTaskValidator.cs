using System;
using System.Text.RegularExpressions;
using SyncAgent.Core.Abstractions;
using SyncAgent.Core.Configuration;
using SyncAgent.Core.Exceptions;
using SyncAgent.Core.Models;

namespace SyncAgent.Core.Validation
{
    /// <summary>
    /// Validates the envelope of a task: identifier, task type, parameter bulk and timestamp.
    /// </summary>
    /// <remarks>
    /// The platform is treated as an untrusted peer. It is reachable over plain HTTP on
    /// loopback and anything able to bind that port ahead of it, or to sit between the two,
    /// can feed this agent whatever it likes. Everything crossing that boundary is therefore
    /// bounded in size and checked against the documented format before it reaches a handler.
    ///
    /// Per-task-type parameter rules live in <see cref="SyncTaskParameterReader"/>; this type
    /// only knows about what every task has in common.
    /// </remarks>
    public sealed class SyncTaskValidator : ISyncTaskValidator
    {
        /// <summary>
        /// Crockford base32 as used by ULID: ten digits plus twenty-two letters, with I, L, O
        /// and U left out to avoid transcription errors. The contract types taskId as a ULID,
        /// so a 26-character string over that alphabet is the whole of the valid space.
        /// </summary>
        private static readonly Regex UlidPattern = new Regex(
            "^[0-9ABCDEFGHJKMNPQRSTVWXYZ]{26}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private const int MaxTaskTypeLength = 64;
        private const int MaxParameterCount = 16;
        private const int MaxParameterNameLength = 64;
        private const int MaxParameterValueLength = 256;

        private readonly SyncAgentOptions _options;
        private readonly ISystemClock _clock;

        public SyncTaskValidator(SyncAgentOptions options, ISystemClock clock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void EnsureValid(SyncTaskRequest task)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (string.IsNullOrWhiteSpace(task.TaskId))
            {
                throw new SyncTaskValidationException("The task is missing a task identifier.");
            }

            if (!UlidPattern.IsMatch(task.TaskId))
            {
                throw new SyncTaskValidationException("The task identifier is not a valid ULID.");
            }

            if (string.IsNullOrWhiteSpace(task.TaskType))
            {
                throw new SyncTaskValidationException("The task is missing a task type.");
            }

            if (task.TaskType.Length > MaxTaskTypeLength)
            {
                throw new SyncTaskValidationException("The task type is longer than the contract allows.");
            }

            if (task.Parameters.Count > MaxParameterCount)
            {
                throw new SyncTaskValidationException("The task carries more parameters than the contract allows.");
            }

            foreach (var parameter in task.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key) || parameter.Key.Length > MaxParameterNameLength)
                {
                    throw new SyncTaskValidationException("The task carries a parameter with an invalid name.");
                }

                if (parameter.Value != null && parameter.Value.Length > MaxParameterValueLength)
                {
                    throw new SyncTaskValidationException("The task carries a parameter value that is too long.");
                }
            }

            // A creation timestamp from the future means the two clocks disagree by more than
            // the configured tolerance. That is worth surfacing on its own, because every
            // incremental cutoff the platform sends is derived from its clock.
            var latestAcceptable = _clock.UtcNow + _options.ClockSkewTolerance;
            if (task.CreatedAt > latestAcceptable)
            {
                throw new SyncTaskValidationException(
                    "The task creation timestamp is further in the future than the allowed clock skew.");
            }
        }
    }
}
