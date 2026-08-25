using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>Hidden HWND_MESSAGE window: the single place where work happens.</summary>
internal sealed class AppWindow : IDisposable
{
    private const string ClassName = "DesktopIconsToggleWnd";

    private readonly Win32.WndProc _wndProc;    // kept alive for the OS
    private readonly IntPtr _classNamePtr;

    public AppWindow()
    {
        _wndProc = WindowProc;
        _classNamePtr = Marshal.StringToHGlobalUni(ClassName);

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = Win32.GetModuleHandleW(null),
            lpszClassName = _classNamePtr,
        };

        if (Win32.RegisterClassExW(ref wc) == 0)
        {
            throw new InvalidOperationException(
                $"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
        }

        Handle = Win32.CreateWindowExW(0, ClassName, ClassName, 0, 0, 0, 0, 0,
                                       Win32.HWND_MESSAGE, IntPtr.Zero,
                                       Win32.GetModuleHandleW(null), IntPtr.Zero);

        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }
    }

    public IntPtr Handle { get; }

    /// <summary>Return true to swallow the message.</summary>
    public event Func<uint, IntPtr, IntPtr, bool>? MessageReceived;

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (MessageReceived?.Invoke(msg, wParam, lParam) == true)
            {
                return IntPtr.Zero;
            }
        }
        catch
        {
            // An exception escaping a reverse P/Invoke callback tears the process down
            // without necessarily running ProcessExit, which would leave the user's icons
            // hidden with no app left to restore them. Leave through the normal exit path
            // instead: the message loop returns and Main's Shutdown() runs EnsureVisible.
            Win32.PostQuitMessage(1);
            return IntPtr.Zero;
        }

        if (msg == Win32.WM_DESTROY)
        {
            Win32.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void RunMessageLoop()
    {
        while (Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            Win32.DestroyWindow(Handle);
        }

        if (_classNamePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_classNamePtr);
        }
    }
}
