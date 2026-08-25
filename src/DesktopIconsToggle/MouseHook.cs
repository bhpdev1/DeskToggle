using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Global low-level mouse hook watching for a middle double-click over the desktop.
///
/// The hook stays installed for the whole process lifetime. Installing it only
/// while the desktop has the foreground would drop the first click, because that
/// click is what gives the desktop the foreground in the first place. The cost of
/// staying installed is one integer comparison per mouse event.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private static readonly string[] DesktopClasses =
        ["Progman", "WorkerW", "SHELLDLL_DefView", "SysListView32"];

    private readonly Win32.HookProc _callback;   // kept alive for the OS
    private readonly ClickDetector _detector;
    private readonly IntPtr _targetWindow;
    private IntPtr _hook;

    public MouseHook(IntPtr targetWindow)
    {
        _targetWindow = targetWindow;
        _callback = OnMouseEvent;
        _detector = new ClickDetector((int)Win32.GetDoubleClickTime());
    }

    /// <summary>Diagnostics only: fires on every completed middle double-click.</summary>
    public Action<bool>? Trace { get; set; }

    public bool Install()
    {
        _hook = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, _callback,
                                        Win32.GetModuleHandleW(null), 0);
        return _hook != IntPtr.Zero;
    }

    private IntPtr OnMouseEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Fast path: everything that is not a middle press leaves immediately,
        // with no allocation and no marshalling.
        if (nCode < 0 || (int)wParam != Win32.WM_MBUTTONDOWN)
        {
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (_detector.RegisterClick(Win32.GetTickCount()))
        {
            var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            bool overDesktop = IsOverDesktop(data.pt);
            Trace?.Invoke(overDesktop);

            if (overDesktop)
            {
                // Never call into Explorer from a hook: Windows unhooks slow callbacks.
                Win32.PostMessageW(_targetWindow, Win32.WM_APP_TOGGLE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        // Always let the click through; middle-clicking the desktop does nothing natively.
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool IsOverDesktop(Win32.POINT point)
    {
        IntPtr hWnd = Win32.WindowFromPoint(point);
        for (int depth = 0; hWnd != IntPtr.Zero && depth < 8; depth++)
        {
            if (Array.IndexOf(DesktopClasses, Win32.GetClassName(hWnd)) >= 0)
            {
                return true;
            }

            hWnd = Win32.GetAncestor(hWnd, Win32.GA_PARENT);
        }

        return false;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
