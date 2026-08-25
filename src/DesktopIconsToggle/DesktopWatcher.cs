using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Raises <see cref="Changed"/> when the desktop gains or loses the foreground.
/// Event-driven: no timer, no polling, no wake-ups while nothing happens.
/// </summary>
internal sealed class DesktopWatcher : IDisposable
{
    // Held in a field so the GC never collects the callback the OS holds.
    private readonly Win32.WinEventProc _callback;
    private IntPtr _hook;

    public DesktopWatcher() => _callback = OnForegroundChanged;

    public event Action<bool>? Changed;

    public bool IsDesktopForeground { get; private set; }

    public static bool IsDesktopWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        string className = Win32.GetClassName(hWnd);
        return className is "Progman" or "WorkerW";
    }

    public void Start()
    {
        IsDesktopForeground = IsDesktopWindow(Win32.GetForegroundWindow());
        _hook = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                      IntPtr.Zero, _callback, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        Changed?.Invoke(IsDesktopForeground);
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                     int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // OBJID_WINDOW == 0; ignore per-control focus noise.
        if (idObject != 0)
        {
            return;
        }

        bool isDesktop = IsDesktopWindow(hwnd);
        if (isDesktop == IsDesktopForeground)
        {
            return;
        }

        IsDesktopForeground = isDesktop;
        Changed?.Invoke(isDesktop);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
