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

    /// <summary>The taskbar's auto-hide state right now, read from the shell.</summary>
    public bool IsAutoHidden => (GetState() & (uint)Win32.ABS_AUTOHIDE) != 0;

    /// <summary>Reads the taskbar's current auto-hide state and remembers it.</summary>
    public void CaptureOriginalState()
    {
        OriginalAutoHide = IsAutoHidden;
    }

    /// <summary>Turns auto-hide on or off, preserving the always-on-top bit.</summary>
    public void SetAutoHide(bool autoHide)
    {
        uint state = GetState();
        uint newState = autoHide
            ? state | (uint)Win32.ABS_AUTOHIDE
            : state & ~(uint)Win32.ABS_AUTOHIDE;

        var data = new Win32.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
            lParam = (IntPtr)(int)newState,
        };

        Win32.SHAppBarMessage(Win32.ABM_SETSTATE, ref data);
    }

    /// <summary>Puts the auto-hide state back to what CaptureOriginalState saw.</summary>
    public void RestoreOriginal()
    {
        SetAutoHide(OriginalAutoHide);
    }

    private static uint GetState()
    {
        var data = new Win32.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
        };

        return (uint)Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref data).ToInt32();
    }
}
