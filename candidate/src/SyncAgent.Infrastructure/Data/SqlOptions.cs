namespace SyncAgent.Infrastructure.Data;

public sealed class SqlOptions
{
    /// <summary>Read from the SYNCAGENT_DB_CONNECTION environment variable — never from
    /// appsettings.json, so it can't end up committed to source control. Settable (not init)
    /// because it's assigned via an Options `Configure` delegate after binding, not through an
    /// object initializer.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; init; } = 30;
}
