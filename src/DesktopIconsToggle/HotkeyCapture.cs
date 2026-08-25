using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Modal "press a key combination" dialog used to pick the global hotkey. The chosen
/// combination is round-tripped through a temporary RegisterHotKey call before it is
/// accepted, so the user can never end up with a combination that silently does nothing.
/// </summary>
internal static class HotkeyCapture
{
    private const string ClassName = "DesktopIconsToggleHotkeyCaptureWnd";
    private const int TempHotKeyId = 2;
    private const int WindowWidth = 460;
    private const int WindowHeight = 170;

    // The window class only needs to be registered once for the process's lifetime, and
    // reused for every call — TryCapture is modal (it blocks until the dialog closes), so
    // there is never more than one capture window alive at a time.
    private static readonly Win32.WndProc s_wndProc = WindowProc; // kept alive for the OS
    private static bool s_classRegistered;

    private static IntPtr s_hwnd;
    private static IntPtr s_statusHandle;
    private static bool s_accepted;
    private static uint s_resultModifiers;
    private static uint s_resultVirtualKey;

    /// <summary>
    /// Shows a modal window asking the user to press a key combination, and returns
    /// true with the chosen combination when they pick one that Windows will accept.
    /// Returns false when the user cancels or closes the window.
    /// The caller must unregister its own hotkey first, otherwise the user cannot
    /// re-pick the combination they are already using.
    /// </summary>
    public static bool TryCapture(IntPtr owner, out uint modifiers, out uint virtualKey)
    {
        if (s_hwnd != IntPtr.Zero)
        {
            // The nested loop below keeps the tray path live, so the menu can re-enter
            // here. A second window would orphan the first: the close handler acts on
            // this static handle and would no longer reach it.
            Win32.SetForegroundWindow(s_hwnd);
            modifiers = 0;
            virtualKey = 0;
            return false;
        }

        s_accepted = false;
        s_resultModifiers = 0;
        s_resultVirtualKey = 0;
        s_hwnd = IntPtr.Zero;
        s_statusHandle = IntPtr.Zero;

        EnsureClassRegistered();
        CreateCaptureWindow(owner);

        Win32.ShowWindow(s_hwnd, Win32.SW_SHOW);
        Win32.UpdateWindow(s_hwnd);
        Win32.SetForegroundWindow(s_hwnd);
        Win32.SetFocus(s_hwnd);

        while (s_hwnd != IntPtr.Zero)
        {
            int result = Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0);

            if (result == 0)
            {
                // WM_QUIT: the app is shutting down while the dialog is open. Re-post it
                // so the main window's message loop still sees it, then unwind without
                // accepting — swallowing it here would hang the app on exit.
                Win32.PostQuitMessage(0);
                CloseWindow(accepted: false);
                break;
            }

            if (result < 0)
            {
                break;
            }

            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }

        // Safety net for exit paths that break out of the loop without going through
        // CloseWindow (e.g. a GetMessageW failure) — destroy the window and release the
        // temporary hotkey id regardless of how the loop ended. No-op if already closed.
        CloseWindow(accepted: s_accepted);

        modifiers = s_resultModifiers;
        virtualKey = s_resultVirtualKey;
        return s_accepted;
    }

    private static void EnsureClassRegistered()
    {
        if (s_classRegistered)
        {
            return;
        }

        IntPtr classNamePtr = Marshal.StringToHGlobalUni(ClassName);

        try
        {
            var wc = new Win32.WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                hInstance = Win32.GetModuleHandleW(null),
                lpszClassName = classNamePtr,
                hbrBackground = new IntPtr(Win32.COLOR_BTNFACE + 1),
                hCursor = Win32.LoadCursorW(IntPtr.Zero, new IntPtr(Win32.IDC_ARROW)),
            };

            if (Win32.RegisterClassExW(ref wc) == 0)
            {
                throw new InvalidOperationException(
                    $"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
            }

            s_classRegistered = true;
        }
        finally
        {
            // RegisterClassEx copies the class name into its own table; the pointer is
            // only needed for the duration of the call above.
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    private static void CreateCaptureWindow(IntPtr owner)
    {
        IntPtr hInstance = Win32.GetModuleHandleW(null);

        int screenWidth = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int screenHeight = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
        int x = Math.Max(0, (screenWidth - WindowWidth) / 2);
        int y = Math.Max(0, (screenHeight - WindowHeight) / 2);

        // Caption + system menu (gives the close button) but no WS_THICKFRAME and no
        // WS_MINIMIZEBOX/WS_MAXIMIZEBOX: not resizable, not minimisable.
        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;

        s_hwnd = Win32.CreateWindowExW(0, ClassName, "Raccourci clavier", style,
                                       x, y, WindowWidth, WindowHeight,
                                       owner, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (s_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        const uint labelStyle = Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_CENTER;

        IntPtr prompt = Win32.CreateWindowExW(0, "STATIC",
            "Appuie sur la combinaison souhaitée.", labelStyle,
            15, 12, WindowWidth - 30, 20, s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        IntPtr hint = Win32.CreateWindowExW(0, "STATIC",
            "Au moins une touche de modification (Ctrl, Alt, Maj ou Windows) est requise. Échap seul annule.",
            labelStyle, 15, 38, WindowWidth - 30, 34, s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        s_statusHandle = Win32.CreateWindowExW(0, "STATIC", string.Empty, labelStyle,
            15, 84, WindowWidth - 30, 46, s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        IntPtr font = Win32.GetStockObject(Win32.DEFAULT_GUI_FONT);
        Win32.SendMessageW(prompt, Win32.WM_SETFONT, font, new IntPtr(1));
        Win32.SendMessageW(hint, Win32.WM_SETFONT, font, new IntPtr(1));
        Win32.SendMessageW(s_statusHandle, Win32.WM_SETFONT, font, new IntPtr(1));
    }

    private static void CloseWindow(bool accepted)
    {
        s_accepted = accepted;

        if (s_hwnd != IntPtr.Zero)
        {
            Win32.UnregisterHotKey(s_hwnd, TempHotKeyId);
            Win32.DestroyWindow(s_hwnd);
            s_hwnd = IntPtr.Zero;
        }
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN)
            {
                HandleKeyDown((uint)wParam.ToInt64());
                return IntPtr.Zero;
            }

            if (msg == Win32.WM_CLOSE)
            {
                CloseWindow(accepted: false);
                return IntPtr.Zero;
            }
        }
        catch
        {
            // An exception escaping a reverse P/Invoke callback tears the process down.
            // Fall back to a plain cancel instead of taking the whole app with it.
            CloseWindow(accepted: false);
            return IntPtr.Zero;
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void HandleKeyDown(uint vk)
    {
        uint mods = ReadModifiers();

        if (IsModifierKey(vk))
        {
            SetStatus(HeldModifiersText(mods) + "…");
            return;
        }

        if (vk == Win32.VK_ESCAPE && mods == 0)
        {
            CloseWindow(accepted: false);
            return;
        }

        // Alt+F4 means "close this window" everywhere in Windows. RegisterHotKey would
        // grant it (it is not system-reserved), and the app would then swallow it
        // system-wide, breaking window closing across the whole session. Treat it as a
        // cancel instead — the same path bare Escape uses.
        if (vk == Win32.VK_F4 && mods == Win32.MOD_ALT)
        {
            CloseWindow(accepted: false);
            return;
        }

        if (mods == 0)
        {
            SetStatus("Il faut au moins une touche de modification.");
            return;
        }

        if (Win32.RegisterHotKey(s_hwnd, TempHotKeyId, mods | Win32.MOD_NOREPEAT, vk))
        {
            Win32.UnregisterHotKey(s_hwnd, TempHotKeyId);
            s_resultModifiers = mods;
            s_resultVirtualKey = vk;
            CloseWindow(accepted: true);
            return;
        }

        SetStatus(HotkeyFormatter.Format(mods, vk) + " — déjà utilisée par le système ou une autre application.");
    }

    private static uint ReadModifiers()
    {
        uint mods = 0;

        if (IsKeyDown(Win32.VK_CONTROL))
        {
            mods |= Win32.MOD_CONTROL;
        }

        if (IsKeyDown(Win32.VK_MENU))
        {
            mods |= Win32.MOD_ALT;
        }

        if (IsKeyDown(Win32.VK_SHIFT))
        {
            mods |= Win32.MOD_SHIFT;
        }

        if (IsKeyDown(Win32.VK_LWIN) || IsKeyDown(Win32.VK_RWIN))
        {
            mods |= Win32.MOD_WIN;
        }

        return mods;
    }

    private static bool IsKeyDown(uint virtualKey) => (Win32.GetKeyState((int)virtualKey) & 0x8000) != 0;

    private static bool IsModifierKey(uint vk) =>
        vk == Win32.VK_CONTROL || vk == Win32.VK_MENU || vk == Win32.VK_SHIFT ||
        vk == Win32.VK_LWIN || vk == Win32.VK_RWIN;

    private static string HeldModifiersText(uint mods)
    {
        var parts = new List<string>(4);

        if ((mods & Win32.MOD_WIN) != 0)
        {
            parts.Add("Win");
        }

        if ((mods & Win32.MOD_CONTROL) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((mods & Win32.MOD_ALT) != 0)
        {
            parts.Add("Alt");
        }

        if ((mods & Win32.MOD_SHIFT) != 0)
        {
            parts.Add("Shift");
        }

        return string.Join("+", parts);
    }

    private static void SetStatus(string text)
    {
        if (s_statusHandle != IntPtr.Zero)
        {
            Win32.SetWindowTextW(s_statusHandle, text);
        }
    }
}
