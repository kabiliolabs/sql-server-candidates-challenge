using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using SyncAgent.Core.Abstractions;
using SyncAgent.Infrastructure.Platform;

namespace SyncAgent.Infrastructure.Resilience
{
    /// <summary>
    /// Recognises the platform-side failures that are worth trying again.
    /// </summary>
    public sealed class PlatformTransientErrorDetector : ITransientErrorDetector
    {
        public bool IsTransient(Exception exception)
        {
            switch (exception)
            {
                case PlatformApiException platformException:
                    // Already classified at the throw site, where the status code was in hand.
                    return platformException.IsTransient;

                case HttpRequestException _:
                case SocketException _:
                case IOException _:
                    // The platform is not answering: not started yet, restarting, or the
                    // connection was cut mid-response. All of these clear on their own.
                    return true;

                case OperationCanceledException _:
                    // HttpClient reports its own timeout as a TaskCanceledException with no
                    // cancellation actually requested. The retry executor re-throws the
                    // genuine shutdown case before any detector is consulted, so what reaches
                    // here is a timeout and deserves another attempt.
                    return true;

                default:
                    return false;
            }
        }
    }
}
