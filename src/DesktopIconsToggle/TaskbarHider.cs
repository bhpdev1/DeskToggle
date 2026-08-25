using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Puts the taskbar into (or out of) Windows' own auto-hide mode via the shell
/// appbar API. Once auto-hide is set, Windows itself handles revealing the
/// taskbar at the screen edge: no polling, no timer, no latency.
/// </summary>
internal sealed class TaskbarHider
{
    public bool OriginalAutoHide { get; private set; }

    /// <summary>True once a trustworthy original state has been read from the shell.</summary>
    public bool HasOriginalState { get; private set; }

    /// <summary>The taskbar's auto-hide state right now, read from the shell.</summary>
    public bool IsAutoHidden => (GetState() & (uint)Win32.ABS_AUTOHIDE) != 0;

    /// <summary>
    /// Reads and remembers the taskbar's auto-hide state. Returns false when the shell
    /// has not created the taskbar yet — common when the app is launched from the Run key
    /// at logon. ABM_GETSTATE would answer 0, which is indistinguishable from "auto-hide
    /// is off", and acting on that would switch a real user's setting off permanently.
    /// </summary>
    public bool CaptureOriginalState()
    {
        if (Win32.FindWindowW("Shell_TrayWnd", null) == IntPtr.Zero)
        {
            return false;
        }

        OriginalAutoHide = (GetState() & (uint)Win32.ABS_AUTOHIDE) != 0;
        HasOriginalState = true;
        return true;
    }

    /// <summary>Turns auto-hide on or off, preserving the always-on-top bit.</summary>
    public bool SetAutoHide(bool autoHide)
    {
        uint newState = ApplyAutoHide(GetState(), autoHide);

        var data = new Win32.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
            lParam = (IntPtr)(int)newState,
        };

        Win32.SHAppBarMessage(Win32.ABM_SETSTATE, ref data);

        // ABM_SETSTATE always returns TRUE, so the only real confirmation is a read-back.
        return ((GetState() & (uint)Win32.ABS_AUTOHIDE) != 0) == autoHide;
    }

    /// <summary>
    /// Puts the auto-hide state back to what CaptureOriginalState saw. A no-op — and
    /// reported as unconfirmed — when the original state was never trustworthily read,
    /// since acting on an unknown original is exactly what must not happen.
    /// </summary>
    public bool RestoreOriginal() => HasOriginalState && SetAutoHide(OriginalAutoHide);

    /// <summary>Sets or clears ABS_AUTOHIDE in <paramref name="state"/> without touching any other bit.</summary>
    internal static uint ApplyAutoHide(uint state, bool autoHide)
        => autoHide
            ? state | (uint)Win32.ABS_AUTOHIDE
            : state & ~(uint)Win32.ABS_AUTOHIDE;

    private static uint GetState()
    {
        var data = new Win32.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
        };

        return (uint)Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref data).ToInt32();
    }
}
