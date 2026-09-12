namespace SyncAgent.Application.Services;

/// <summary>
/// Tracks whether the sync platform was reachable on the previous poll so that an ongoing outage
/// is reported once rather than on every cycle. Register as a Singleton: the poller that uses it is
/// Scoped and rebuilt each cycle, so the state cannot live there.
/// </summary>
public interface ISyncPlatformConnectivityTracker
{
    /// <summary>Records a failed attempt. Returns true only the first time in a row this happens (a new outage).</summary>
    bool RecordFailure();

    /// <summary>Records a successful attempt. Returns true only if it follows one or more failures (a recovery).</summary>
    bool RecordSuccess();
}

public sealed class SyncPlatformConnectivityTracker : ISyncPlatformConnectivityTracker
{
    private bool _isUnreachable;

    public bool RecordFailure()
    {
        if (_isUnreachable)
        {
            return false;
        }

        _isUnreachable = true;
        return true;
    }

    public bool RecordSuccess()
    {
        if (!_isUnreachable)
        {
            return false;
        }

        _isUnreachable = false;
        return true;
    }
}
