namespace Vernuko.Security;

/// <summary>Locks the session after a period of inactivity (front-desk PCs are often left unattended).</summary>
public sealed class IdleLockPolicy
{
    private readonly TimeProvider _time;
    private DateTimeOffset _lastActivity;

    public IdleLockPolicy(TimeProvider time, TimeSpan idleTimeout)
    {
        _time = time;
        IdleTimeout = idleTimeout;
        _lastActivity = time.GetUtcNow();
    }

    public TimeSpan IdleTimeout { get; set; }

    public void RegisterActivity() => _lastActivity = _time.GetUtcNow();

    public bool ShouldLock() => IdleTimeout > TimeSpan.Zero && _time.GetUtcNow() - _lastActivity >= IdleTimeout;
}
