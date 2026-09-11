using System;

namespace SyncAgent
{
    /// <summary>Binds the "Platform" config section plus ConnectionStrings:AdventureWorks.</summary>
    public sealed class AgentOptions
    {
        public string ConnectionString { get; set; }
        public string Url { get; set; }
        public string ApiKey { get; set; }
        public int PollIntervalMs { get; set; } = 5000;
        public int MaxRows { get; set; } = 10000;
        public int CommandTimeoutSeconds { get; set; } = 30;

        /// <summary>Fail fast: an invalid config must stop startup, not pollute the first run.</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new InvalidOperationException(
                    "Connection string missing: set ConnectionStrings:AdventureWorks.");
            if (string.IsNullOrWhiteSpace(Url) ||
                !Uri.TryCreate(Url, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException(
                    "Platform:Url must be an absolute http(s) URL.");
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("Platform:ApiKey must be set.");
            if (PollIntervalMs <= 0)
                throw new InvalidOperationException("Platform:PollIntervalMs must be positive.");
            if (MaxRows <= 0)
                throw new InvalidOperationException("Platform:MaxRows must be positive.");
            if (CommandTimeoutSeconds <= 0)
                throw new InvalidOperationException("Platform:CommandTimeoutSeconds must be positive.");
        }
    }
}