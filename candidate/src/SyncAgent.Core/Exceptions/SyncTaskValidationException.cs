using System;
using System.Runtime.Serialization;

namespace SyncAgent.Core.Exceptions
{
    /// <summary>
    /// Raised when a task received from the platform fails validation.
    /// </summary>
    /// <remarks>
    /// Messages carried by this exception are written to be safe to send back to the
    /// platform: they describe what was wrong with the input and never echo internal state
    /// or the offending value itself.
    /// </remarks>
    [Serializable]
    public class SyncTaskValidationException : Exception
    {
        public SyncTaskValidationException()
        {
        }

        public SyncTaskValidationException(string message)
            : base(message)
        {
        }

        public SyncTaskValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SyncTaskValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
