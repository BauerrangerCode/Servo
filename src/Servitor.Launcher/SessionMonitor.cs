namespace Servitor.Launcher;

/// <summary>
/// Decides when Servo has nothing left to serve. The page reports itself (<see cref="Heartbeat"/> every few seconds,
/// <see cref="Closing"/> when it is unloaded), because the browser process that Servo starts cannot be trusted to
/// live as long as the window: on the first start with a fresh profile Edge and Chrome restart themselves and the
/// started process ends within seconds while the window stays.
/// </summary>
public sealed class SessionMonitor
{
    /// <summary>How long the browser gets to load the app before Servo gives up.</summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(90);

    /// <summary>The wait after a page reports being unloaded; a reload sends a heartbeat within this time.</summary>
    public static readonly TimeSpan CloseGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Without any report for this long the window is considered gone (crash, killed browser). Generous, because
    /// browsers slow timers of hidden or minimized windows down to about one a minute.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(150);

    private readonly TimeProvider _time;
    private readonly DateTimeOffset _started;
    private readonly object _lock = new();
    private DateTimeOffset? _lastHeartbeat;
    private DateTimeOffset? _closingAt;
    private bool _quitRequested;

    public SessionMonitor(TimeProvider time)
    {
        _time = time;
        _started = time.GetUtcNow();
    }

    public void Heartbeat()
    {
        lock (_lock)
        {
            _lastHeartbeat = _time.GetUtcNow();
            _closingAt = null;
        }
    }

    public void Closing()
    {
        lock (_lock)
        {
            _closingAt = _time.GetUtcNow() + CloseGrace;
        }
    }

    /// <summary>The user chose "Close app": stop at once, without waiting for the page to go quiet.</summary>
    public void Quit()
    {
        lock (_lock)
        {
            _quitRequested = true;
        }
    }

    public bool QuitRequested
    {
        get
        {
            lock (_lock)
            {
                return _quitRequested;
            }
        }
    }

    public bool ShouldExit()
    {
        lock (_lock)
        {
            if (_quitRequested)
            {
                return true;
            }

            var now = _time.GetUtcNow();
            if (_lastHeartbeat is null)
            {
                return now - _started > StartupTimeout;
            }

            return (_closingAt is not null && now >= _closingAt) || now - _lastHeartbeat > StaleAfter;
        }
    }
}
