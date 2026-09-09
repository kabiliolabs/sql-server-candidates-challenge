using System;
using System.Runtime.Serialization;

namespace SyncAgent.Core.Configuration
{
    /// <summary>
    /// Raised when the agent is started with configuration it cannot run on.
    /// </summary>
    /// <remarks>
    /// Always fatal and always thrown before any polling begins. A misconfigured agent that
    /// starts anyway and fails one task at a time is far harder to diagnose than one that
    /// refuses to start and says why.
    /// </remarks>
    [Serializable]
    public sealed class SyncAgentConfigurationException : Exception
    {
        public SyncAgentConfigurationException()
        {
        }

        public SyncAgentConfigurationException(string message)
            : base(message)
        {
        }

        public SyncAgentConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private SyncAgentConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
