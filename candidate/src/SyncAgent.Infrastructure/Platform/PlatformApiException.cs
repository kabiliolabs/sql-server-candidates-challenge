using System;
using System.Net;
using System.Runtime.Serialization;

namespace SyncAgent.Infrastructure.Platform
{
    /// <summary>
    /// The platform answered, but not with something the agent can use.
    /// </summary>
    /// <remarks>
    /// <see cref="IsTransient"/> is decided at the throw site, where the status code is known,
    /// rather than being re-derived by the retry policy from a parsed message. A 500 or a 408
    /// is worth retrying; a 400 or a 401 will fail identically every time and retrying it only
    /// delays the failure and adds load to a platform that already said no.
    /// </remarks>
    [Serializable]
    public sealed class PlatformApiException : Exception
    {
        public PlatformApiException()
        {
        }

        public PlatformApiException(string message)
            : base(message)
        {
        }

        public PlatformApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public PlatformApiException(string message, HttpStatusCode statusCode, bool isTransient)
            : base(message)
        {
            StatusCode = statusCode;
            IsTransient = isTransient;
        }

        private PlatformApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            StatusCode = (HttpStatusCode)info.GetInt32(nameof(StatusCode));
            IsTransient = info.GetBoolean(nameof(IsTransient));
        }

        public HttpStatusCode StatusCode { get; }

        public bool IsTransient { get; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            base.GetObjectData(info, context);
            info.AddValue(nameof(StatusCode), (int)StatusCode);
            info.AddValue(nameof(IsTransient), IsTransient);
        }
    }
}
