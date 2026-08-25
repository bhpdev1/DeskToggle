namespace DesktopIconsToggle;

/// <summary>
/// Recognises a middle-button double click. Deliberately free of Win32 so it
/// can be tested against an injected clock.
/// </summary>
internal sealed class ClickDetector
{
    private readonly long _windowMs;
    private long _lastClickMs;
    private bool _armed;

    public ClickDetector(int doubleClickTimeMs)
        => _windowMs = (long)(doubleClickTimeMs * 1.5);

    /// <summary>Returns true on the click that completes a pair.</summary>
    public bool RegisterClick(long nowMs)
    {
        long sinceLastClick = nowMs - _lastClickMs;

        // sinceLastClick < 0 means the caller's clock went backwards — GetTickCount
        // wraps to zero about every 49.7 days. Treat it as a fresh first click
        // rather than letting a negative delta satisfy the window test.
        if (_armed && sinceLastClick >= 0 && sinceLastClick <= _windowMs)
        {
            _armed = false;
            return true;
        }

        _armed = true;
        _lastClickMs = nowMs;
        return false;
    }
}
