using System;
using System.Runtime.Serialization;

namespace SyncAgent.Core.Exceptions
{
    /// <summary>
    /// Raised when the platform asks for a task type no handler is registered for.
    /// </summary>
    /// <remarks>
    /// Treated as a validation failure rather than a crash: an agent that predates a newly
    /// introduced task type must keep draining the queue and report that one task as failed,
    /// not fall over. The offending value is never interpolated into the message that leaves
    /// the process, because it is text an attacker could control.
    /// </remarks>
    [Serializable]
    public sealed class UnknownTaskTypeException : SyncTaskValidationException
    {
        public const string SanitizedMessage = "The requested task type is not supported by this agent.";

        public UnknownTaskTypeException()
            : base(SanitizedMessage)
        {
        }

        public UnknownTaskTypeException(string message)
            : base(message)
        {
        }

        public UnknownTaskTypeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private UnknownTaskTypeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
