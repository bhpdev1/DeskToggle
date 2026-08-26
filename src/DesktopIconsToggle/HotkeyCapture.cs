using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Modal "press a key combination" dialog used to pick the global hotkey. The chosen
/// combination is round-tripped through a temporary RegisterHotKey call before it is
/// accepted, so the user can never end up with a combination that silently does nothing.
/// A confirmation step then lets the user see the combination in large type and either
/// accept it, cancel, or try another one before it is returned to the caller.
/// </summary>
internal static class HotkeyCapture
{
    private const string ClassName = "DesktopIconsToggleHotkeyCaptureWnd";
    private const int TempHotKeyId = 2;
    private const int IdConfirmButton = 1001;
    private const int IdCancelButton = 1002;

    private enum CaptureState { Capturing, Confirming }

    // The window class only needs to be registered once for the process's lifetime, and
    // reused for every call — TryCapture is modal (it blocks until the dialog closes), so
    // there is never more than one capture window alive at a time.
    private static readonly Win32.WndProc s_wndProc = WindowProc; // kept alive for the OS
    private static bool s_classRegistered;

    private static IntPtr s_hwnd;
    private static IntPtr s_promptHandle;
    private static IntPtr s_hintHandle;
    private static IntPtr s_comboHandle;
    private static IntPtr s_statusHandle;
    private static IntPtr s_confirmButtonHandle;
    private static IntPtr s_cancelButtonHandle;

    private static SystemFont? s_font;
    private static SystemFont? s_comboFont;
    private static Theme? s_theme;
    private static IntPtr s_backgroundBrush;

    private static CaptureState s_state;
    private static uint s_candidateModifiers;
    private static uint s_candidateVirtualKey;

    private static bool s_accepted;
    private static uint s_resultModifiers;
    private static uint s_resultVirtualKey;

    /// <summary>
    /// Shows a modal window asking the user to press a key combination, and returns
    /// true with the chosen combination once the user confirms it. Returns false when
    /// the user cancels or closes the window.
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
        s_candidateModifiers = 0;
        s_candidateVirtualKey = 0;
        s_state = CaptureState.Capturing;
        s_hwnd = IntPtr.Zero;
        s_promptHandle = IntPtr.Zero;
        s_hintHandle = IntPtr.Zero;
        s_comboHandle = IntPtr.Zero;
        s_statusHandle = IntPtr.Zero;
        s_confirmButtonHandle = IntPtr.Zero;
        s_cancelButtonHandle = IntPtr.Zero;

        // Resolved once, early, before anything else: the monitor decides the DPI, and
        // both must be settled before layout or window creation touches either. Resolving
        // them separately (system DPI now, monitor-under-cursor later) is exactly the bug
        // this fixes — on a mixed-DPI multi-monitor setup the two can disagree.
        IntPtr monitor = ResolveMonitor();
        int dpi = ResolveDpi(monitor);

        try
        {
            // Read fresh every call rather than cached: the user may have flipped Windows'
            // light/dark preference since the last time this dialog was shown. Created
            // inside the try so a registry failure other than the two IThemeSource guards
            // against (UnauthorizedAccessException, SecurityException) still lands in this
            // method's own cleanup instead of escaping TryCapture entirely.
            s_theme = new Theme(new RegistryThemeSource());
            s_backgroundBrush = Win32.CreateSolidBrush(s_theme.Background);

            EnsureClassRegistered();
            CreateCaptureWindow(owner, monitor, dpi);

            Win32.ShowWindow(s_hwnd, Win32.SW_SHOW);
            Win32.UpdateWindow(s_hwnd);
            Win32.SetForegroundWindow(s_hwnd);
            Win32.SetFocus(s_hwnd);

            while (s_hwnd != IntPtr.Zero)
            {
                int result = Win32.GetMessageW(out var msg, IntPtr.Zero, 0, 0);

                if (result == 0)
                {
                    // WM_QUIT: the app is shutting down while the dialog is open. Re-post
                    // it so the main window's message loop still sees it, then unwind
                    // without accepting — swallowing it here would hang the app on exit.
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
        }
        finally
        {
            // Safety net for every exit path that does not already go through
            // CloseWindow — a GetMessageW failure, or an exception thrown while the
            // window/controls were still being set up (e.g. CreateWindowExW failing for
            // the main window, after the theme brush above was already allocated).
            // CloseWindow is idempotent — a window already destroyed, or fonts/brush
            // already disposed, are simply no-ops — so this is safe to run unconditionally.
            CloseWindow(accepted: s_accepted);
        }

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
                // No class brush: the background depends on the current theme, which can
                // change between launches, so WM_ERASEBKGND paints it instead.
                hbrBackground = IntPtr.Zero,
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

    private static void CreateCaptureWindow(IntPtr owner, IntPtr monitor, int dpi)
    {
        IntPtr hInstance = Win32.GetModuleHandleW(null);

        // Caption + system menu (gives the close button) but no WS_THICKFRAME and no
        // WS_MINIMIZEBOX/WS_MAXIMIZEBOX: not resizable, not minimisable.
        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;
        const uint exStyle = 0;

        // DialogLayout.WindowSize is the desired client size. CreateWindowExW wants the
        // outer size, which is larger by the caption and borders — passing the client
        // size straight through there is exactly the bug this fixes, moved to the other
        // axis from the one the client-rect re-read below fixes for.
        (int desiredClientWidth, int desiredClientHeight) = DialogLayout.WindowSize(dpi);

        var outerRect = new Win32.RECT { Left = 0, Top = 0, Right = desiredClientWidth, Bottom = desiredClientHeight };
        Win32.AdjustWindowRectExForDpi(ref outerRect, style, false, exStyle, (uint)dpi);
        int outerWidth = outerRect.Right - outerRect.Left;
        int outerHeight = outerRect.Bottom - outerRect.Top;

        (int x, int y) = ComputeCenteredPosition(monitor, outerWidth, outerHeight);

        s_hwnd = Win32.CreateWindowExW(exStyle, ClassName, "Raccourci clavier", style,
                                       x, y, outerWidth, outerHeight,
                                       owner, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (s_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }

        ApplyWindows11Chrome();

        s_font = new SystemFont(dpi);
        s_comboFont = new SystemFont(dpi, extraPoints: 6);

        // Re-read the actual client rect rather than trusting desiredClientWidth/Height
        // above: AdjustWindowRectExForDpi's outer size is what was requested, not
        // necessarily what Windows granted, so laying out controls against the size we
        // asked for instead of the size we got is the same class of mismatch this method
        // exists to avoid.
        Win32.GetClientRect(s_hwnd, out var clientRect);
        int clientWidth = clientRect.Right - clientRect.Left;
        int clientHeight = clientRect.Bottom - clientRect.Top;

        var promptRect = DialogLayout.Prompt(clientWidth, dpi);
        var hintRect = DialogLayout.Hint(clientWidth, dpi);
        var comboRect = DialogLayout.Combo(clientWidth, dpi);
        var statusRect = DialogLayout.Status(clientWidth, dpi);
        var confirmRect = DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi);
        var cancelRect = DialogLayout.CancelButton(clientWidth, clientHeight, dpi);

        const uint labelStyle = Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_CENTER;

        s_promptHandle = Win32.CreateWindowExW(0, "STATIC",
            "Appuie sur la combinaison souhaitée.", labelStyle,
            promptRect.X, promptRect.Y, promptRect.Width, promptRect.Height,
            s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        s_hintHandle = Win32.CreateWindowExW(0, "STATIC",
            "Au moins un modificateur (Ctrl, Alt, Maj ou Windows) est requis. Échap seul annule.",
            labelStyle, hintRect.X, hintRect.Y, hintRect.Width, hintRect.Height,
            s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        s_comboHandle = Win32.CreateWindowExW(0, "STATIC", string.Empty, labelStyle,
            comboRect.X, comboRect.Y, comboRect.Width, comboRect.Height,
            s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        s_statusHandle = Win32.CreateWindowExW(0, "STATIC", string.Empty, labelStyle,
            statusRect.X, statusRect.Y, statusRect.Width, statusRect.Height,
            s_hwnd, IntPtr.Zero, hInstance, IntPtr.Zero);

        const uint buttonStyle = Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_PUSHBUTTON;

        s_confirmButtonHandle = Win32.CreateWindowExW(0, "BUTTON", "Confirmer", buttonStyle,
            confirmRect.X, confirmRect.Y, confirmRect.Width, confirmRect.Height,
            s_hwnd, new IntPtr(IdConfirmButton), hInstance, IntPtr.Zero);

        s_cancelButtonHandle = Win32.CreateWindowExW(0, "BUTTON", "Annuler", buttonStyle,
            cancelRect.X, cancelRect.Y, cancelRect.Width, cancelRect.Height,
            s_hwnd, new IntPtr(IdCancelButton), hInstance, IntPtr.Zero);

        // Standard BUTTON controls do not follow DWMWA_USE_IMMERSIVE_DARK_MODE (that only
        // darkens the frame) or WM_CTLCOLORSTATIC (buttons aren't STATIC controls): without
        // this they stay light-themed on a dark background. HRESULT ignored — it fails
        // harmlessly on Windows builds that do not know the "DarkMode_Explorer" class.
        ApplyButtonTheme(s_confirmButtonHandle);
        ApplyButtonTheme(s_cancelButtonHandle);

        // Created once, up front — not on each state transition — then hidden until a
        // combination has actually passed the probe and there is something to confirm.
        Win32.ShowWindow(s_confirmButtonHandle, Win32.SW_HIDE);
        Win32.ShowWindow(s_cancelButtonHandle, Win32.SW_HIDE);

        Win32.SendMessageW(s_promptHandle, Win32.WM_SETFONT, s_font.Handle, new IntPtr(1));
        Win32.SendMessageW(s_hintHandle, Win32.WM_SETFONT, s_font.Handle, new IntPtr(1));
        Win32.SendMessageW(s_statusHandle, Win32.WM_SETFONT, s_font.Handle, new IntPtr(1));
        Win32.SendMessageW(s_confirmButtonHandle, Win32.WM_SETFONT, s_font.Handle, new IntPtr(1));
        Win32.SendMessageW(s_cancelButtonHandle, Win32.WM_SETFONT, s_font.Handle, new IntPtr(1));
        Win32.SendMessageW(s_comboHandle, Win32.WM_SETFONT, s_comboFont.Handle, new IntPtr(1));
    }

    /// <summary>
    /// Resolves the monitor under the cursor, so a multi-monitor user working on their
    /// second screen gets the dialog on that screen rather than the primary one. Resolved
    /// once, early in <see cref="TryCapture"/>, and reused for both the DPI lookup and the
    /// centring below so the two can never disagree about which screen they mean.
    /// </summary>
    private static IntPtr ResolveMonitor()
    {
        Win32.GetCursorPos(out var cursor);
        return Win32.MonitorFromPoint(cursor, Win32.MONITOR_DEFAULTTONEAREST);
    }

    /// <summary>
    /// The given monitor's own effective DPI, not the system (primary monitor) DPI: on a
    /// mixed-DPI multi-monitor setup the two can disagree, and since there is no
    /// WM_DPICHANGED handler here, a wrong DPI at creation time is never corrected later.
    /// Falls back to GetDpiForSystem when the monitor handle is invalid or the per-monitor
    /// lookup fails (checked via its HRESULT), which should not happen in practice but
    /// must not throw if it does.
    /// </summary>
    private static int ResolveDpi(IntPtr monitor)
    {
        if (monitor != IntPtr.Zero &&
            Win32.GetDpiForMonitor(monitor, Win32.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0) // S_OK
        {
            return (int)dpiX;
        }

        return (int)Win32.GetDpiForSystem();
    }

    /// <summary>
    /// Centres the window on the work area of the given (already-resolved) monitor.
    /// Falls back to the primary screen if the monitor info cannot be read, which should
    /// not happen in practice but must not throw if it does.
    /// </summary>
    private static (int X, int Y) ComputeCenteredPosition(IntPtr monitor, int width, int height)
    {
        var info = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };

        if (monitor == IntPtr.Zero || !Win32.GetMonitorInfoW(monitor, ref info))
        {
            int screenWidth = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
            int screenHeight = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
            return (Math.Max(0, (screenWidth - width) / 2), Math.Max(0, (screenHeight - height) / 2));
        }

        Win32.RECT work = info.rcWork;
        int workWidth = work.Right - work.Left;
        int workHeight = work.Bottom - work.Top;

        int x = work.Left + Math.Max(0, (workWidth - width) / 2);
        int y = work.Top + Math.Max(0, (workHeight - height) / 2);
        return (x, y);
    }

    /// <summary>
    /// Rounded corners and (when applicable) a dark title bar. Both attributes are
    /// unsupported before Windows 11 / early dark-mode Windows 10 builds respectively and
    /// return a failure HRESULT there — that failure is expected and deliberately ignored.
    /// </summary>
    private static void ApplyWindows11Chrome()
    {
        int cornerPreference = Win32.DWMWCP_ROUND;
        Win32.DwmSetWindowAttribute(s_hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        if (s_theme!.IsDark)
        {
            int darkMode = 1;
            Win32.DwmSetWindowAttribute(s_hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
    }

    /// <summary>
    /// Switches a standard BUTTON control's visual-style class so it follows the current
    /// theme. Return value ignored: SetWindowTheme fails harmlessly on Windows builds that
    /// do not recognise "DarkMode_Explorer", leaving the button in its default appearance.
    /// </summary>
    private static void ApplyButtonTheme(IntPtr button)
    {
        string themeName = s_theme!.IsDark ? "DarkMode_Explorer" : "Explorer";
        Win32.SetWindowTheme(button, themeName, null);
    }

    private static void CloseWindow(bool accepted)
    {
        s_accepted = accepted;

        if (s_hwnd != IntPtr.Zero)
        {
            Win32.UnregisterHotKey(s_hwnd, TempHotKeyId);
            Win32.DestroyWindow(s_hwnd); // also destroys every child: labels and buttons
            s_hwnd = IntPtr.Zero;
        }

        s_font?.Dispose();
        s_font = null;
        s_comboFont?.Dispose();
        s_comboFont = null;

        if (s_backgroundBrush != IntPtr.Zero)
        {
            Win32.DeleteObject(s_backgroundBrush);
            s_backgroundBrush = IntPtr.Zero;
        }

        s_promptHandle = IntPtr.Zero;
        s_hintHandle = IntPtr.Zero;
        s_comboHandle = IntPtr.Zero;
        s_statusHandle = IntPtr.Zero;
        s_confirmButtonHandle = IntPtr.Zero;
        s_cancelButtonHandle = IntPtr.Zero;
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

            if (msg == Win32.WM_COMMAND)
            {
                HandleCommand(wParam);
                return IntPtr.Zero;
            }

            if (msg == Win32.WM_ERASEBKGND)
            {
                Win32.GetClientRect(hWnd, out var rc);
                Win32.FillRect(wParam, ref rc, s_backgroundBrush);
                return new IntPtr(1);
            }

            if (msg == Win32.WM_CTLCOLORSTATIC || msg == Win32.WM_CTLCOLORBTN)
            {
                // Same (hdc, hwndControl) shape for both messages, and the same fix: paint
                // the control's background/text through the theme rather than leaving the
                // default (light) system colours, which is what created the seam around
                // buttons in dark mode — the buttons themselves are separately re-themed
                // via SetWindowTheme in ApplyButtonTheme.
                return HandleCtlColorStatic(wParam, lParam);
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

    private static void HandleCommand(IntPtr wParam)
    {
        int id = unchecked((int)(long)wParam) & 0xFFFF;

        if (id == IdConfirmButton)
        {
            if (s_state == CaptureState.Confirming)
            {
                Accept();
            }

            return;
        }

        if (id == IdCancelButton)
        {
            CloseWindow(accepted: false);
        }
    }

    private static IntPtr HandleCtlColorStatic(IntPtr wParam, IntPtr lParam)
    {
        if (s_theme is null)
        {
            return IntPtr.Zero;
        }

        IntPtr hdc = wParam;
        IntPtr control = lParam;

        // The prompt and the (large-font) combination use the primary text colour; the
        // hint and status line are deliberately quieter.
        uint textColor = control == s_promptHandle || control == s_comboHandle
            ? s_theme.Text
            : s_theme.SubtleText;

        Win32.SetTextColor(hdc, textColor);
        Win32.SetBkColor(hdc, s_theme.Background);
        return s_backgroundBrush;
    }

    private static void HandleKeyDown(uint vk)
    {
        uint mods = ReadModifiers();

        // Alt+F4 means "close this window" everywhere in Windows. RegisterHotKey would
        // grant it (it is not system-reserved), and the app would then swallow it
        // system-wide, breaking window closing across the whole session. Treat it as a
        // cancel instead — the same path bare Escape uses. Checked before any
        // state-specific logic, so it always cancels rather than being captured.
        if (vk == Win32.VK_F4 && mods == Win32.MOD_ALT)
        {
            CloseWindow(accepted: false);
            return;
        }

        // Bare Escape always cancels. Escape held with a modifier (e.g. Win+Échap) is not
        // "Escape" for this purpose — it is just another candidate combination — so only
        // the no-modifier case is special-cased here.
        if (vk == Win32.VK_ESCAPE && mods == 0)
        {
            CloseWindow(accepted: false);
            return;
        }

        if (s_state == CaptureState.Confirming && vk == Win32.VK_RETURN)
        {
            Accept();
            return;
        }

        if (IsModifierKey(vk))
        {
            // Live feedback on the modifiers held so far only makes sense while still
            // choosing a combination; Confirming already has its own status text.
            if (s_state == CaptureState.Capturing)
            {
                SetStatus(HeldModifiersText(mods) + "…");
            }

            return;
        }

        if (mods == 0)
        {
            SetStatus("Il faut au moins une touche de modification.");
            return;
        }

        if (Win32.RegisterHotKey(s_hwnd, TempHotKeyId, mods | Win32.MOD_NOREPEAT, vk))
        {
            // The probe only needs to prove the combination is grantable; holding it would
            // make re-probing a second candidate below fail against ourselves.
            Win32.UnregisterHotKey(s_hwnd, TempHotKeyId);
            EnterConfirming(mods, vk);
            return;
        }

        SetStatus(HotkeyFormatter.Format(mods, vk) + " — déjà utilisée par le système ou une autre application.");
    }

    /// <summary>
    /// Enters (or, if already there, stays in and updates) the Confirming state. The same
    /// path serves both the initial transition and every subsequent successful re-probe,
    /// since both cases mean "show this newly-validated combination and wait for the
    /// user's decision".
    /// </summary>
    private static void EnterConfirming(uint modifiers, uint virtualKey)
    {
        s_candidateModifiers = modifiers;
        s_candidateVirtualKey = virtualKey;
        s_state = CaptureState.Confirming;

        Win32.SetWindowTextW(s_comboHandle, HotkeyFormatter.Format(modifiers, virtualKey));

        string status = "Entrée pour confirmer, Échap pour annuler. Une autre combinaison remplace celle-ci.";
        if (HotkeyFormatter.IsCommonlyUsedByApps(modifiers, virtualKey))
        {
            status += " Attention : cette combinaison est très utilisée par les applications, elle ne fonctionnera plus ailleurs.";
        }

        SetStatus(status);

        Win32.ShowWindow(s_confirmButtonHandle, Win32.SW_SHOW);
        Win32.ShowWindow(s_cancelButtonHandle, Win32.SW_SHOW);
    }

    private static void Accept()
    {
        s_resultModifiers = s_candidateModifiers;
        s_resultVirtualKey = s_candidateVirtualKey;
        CloseWindow(accepted: true);
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
