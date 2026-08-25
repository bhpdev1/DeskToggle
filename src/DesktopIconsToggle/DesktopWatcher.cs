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

    // The shell's own windows. Clicking our notify icon activates the taskbar, and
    // treating that as "the desktop lost the foreground" would delete the icon out
    // from under the user's cursor before the click callback arrives.
    private static readonly string[] ShellClasses =
        ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
         "TopLevelWindowForOverflowXamlIsland"];

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

    /// <summary>
    /// Installs the foreground hook and publishes the initial state.
    /// Returns false when the hook could not be installed: the caller still gets
    /// the initial state, but will never be told about any later transition.
    /// </summary>
    public bool Start()
    {
        IsDesktopForeground = IsDesktopWindow(Win32.GetForegroundWindow());
        _hook = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                                      IntPtr.Zero, _callback, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);
        Changed?.Invoke(IsDesktopForeground);
        return _hook != IntPtr.Zero;
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                     int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // OBJID_WINDOW == 0; ignore per-control focus noise.
        if (idObject != 0)
        {
            return;
        }

        if (Array.IndexOf(ShellClasses, Win32.GetClassName(hwnd)) >= 0)
        {
            return;
        }

        try
        {
            bool isDesktop = IsDesktopWindow(hwnd);
            if (isDesktop == IsDesktopForeground)
            {
                return;
            }

            IsDesktopForeground = isDesktop;
            Changed?.Invoke(isDesktop);
        }
        catch
        {
            // An exception escaping a reverse P/Invoke callback terminates the process
            // without running ProcessExit, which would strand the user's icons hidden.
            // Swallow it: a missed foreground update beats a lost desktop.
        }
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
