using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>The only component that talks to Explorer.</summary>
internal static class IconToggler
{
    private static readonly Win32.EnumWindowsProc EnumCallback = EnumWorkerW;
    private static IntPtr _workerDefView;

    public static IntPtr FindDefView()
    {
        IntPtr progman = Win32.FindWindowW("Progman", null);
        if (progman != IntPtr.Zero)
        {
            IntPtr direct = Win32.FindWindowExW(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (direct != IntPtr.Zero)
            {
                return direct;
            }
        }

        // Animated wallpapers and some multi-monitor setups reparent the view
        // under a top-level WorkerW instead.
        _workerDefView = IntPtr.Zero;
        Win32.EnumWindows(EnumCallback, IntPtr.Zero);
        return _workerDefView;
    }

    private static bool EnumWorkerW(IntPtr hWnd, IntPtr lParam)
    {
        if (Win32.GetClassName(hWnd) != "WorkerW")
        {
            return true;
        }

        IntPtr child = Win32.FindWindowExW(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (child == IntPtr.Zero)
        {
            return true;
        }

        _workerDefView = child;
        return false;
    }

    /// <summary>
    /// Reads the live window state. The HideIcons registry value is not a
    /// reliable mirror of the toggle message, so it is never consulted.
    /// </summary>
    public static bool AreIconsVisible()
    {
        IntPtr defView = FindDefView();
        if (defView == IntPtr.Zero)
        {
            return true;
        }

        IntPtr list = Win32.FindWindowExW(defView, IntPtr.Zero, "SysListView32", null);
        if (list == IntPtr.Zero)
        {
            // The DefView exists but its list view does not — Explorer is mid-restart.
            // Fail open like the missing-DefView path above: an indeterminate state must
            // never let EnsureVisible post a toggle and hide icons that were visible.
            return true;
        }

        return Win32.IsWindowVisible(list);
    }

    public static bool Toggle()
    {
        IntPtr defView = FindDefView();
        if (defView == IntPtr.Zero)
        {
            return false;
        }

        return Win32.PostMessageW(defView, Win32.WM_COMMAND,
                                  new IntPtr(Win32.TOGGLE_DESKTOP_ICONS), IntPtr.Zero);
    }

    /// <summary>
    /// Safety net used on every exit path. Returns false when the desktop could not be
    /// reached at all, so the caller keeps its recovery marker instead of clearing it.
    /// </summary>
    public static bool EnsureVisible()
    {
        if (FindDefView() == IntPtr.Zero)
        {
            // AreIconsVisible fails open by design, so it would answer "visible" here and
            // hide the fact that we could not act. Report the failure instead.
            return false;
        }

        return AreIconsVisible() || Toggle();
    }
}
