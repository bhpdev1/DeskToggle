namespace DesktopIconsToggle;

/// <summary>
/// Drops toggle requests that arrive too soon after the last accepted one. Explorer needs
/// a moment to apply a posted toggle: without this, a burst of presses makes the desktop
/// flicker, and two presses inside that window would both read the same stale state and
/// leave the recovery marker describing the opposite of the result.
/// </summary>
internal sealed class ToggleThrottle
{
    private readonly long _minIntervalMs;
    private long _lastAcceptedMs;
    private bool _hasAccepted;

    public ToggleThrottle(long minIntervalMs) => _minIntervalMs = minIntervalMs;

    /// <summary>Returns true when the caller should go ahead and toggle.</summary>
    public bool ShouldAccept(long nowMs)
    {
        // A negative delta means the caller's clock went backwards; accept rather than
        // lock the feature out until the clock catches up.
        long sinceLast = nowMs - _lastAcceptedMs;
        if (_hasAccepted && sinceLast >= 0 && sinceLast < _minIntervalMs)
        {
            return false;
        }

        _hasAccepted = true;
        _lastAcceptedMs = nowMs;
        return true;
    }
}
