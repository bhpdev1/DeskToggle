using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

internal readonly record struct TrayMenuState(
    bool IconsVisible,
    bool HideTaskbar,
    string HotkeyLabel,
    bool AutostartEnabled);

internal sealed class TrayIcon : IDisposable
{
    public const int CmdToggle       = 1;
    public const int CmdAutostart    = 2;
    public const int CmdQuit         = 3;
    public const int CmdHideTaskbar  = 4;
    public const int CmdChangeHotkey = 5;

    private const uint IconId = 1;

    private Win32.NOTIFYICONDATAW _data;
    private readonly MenuIcons _menuIcons;
    private bool _visible;

    public TrayIcon(IntPtr ownerWindow)
    {
        IntPtr hIcon = Win32.LoadImageW(Win32.GetModuleHandleW(null),
                                        new IntPtr(Win32.IDI_APPLICATION),
                                        Win32.IMAGE_ICON, 0, 0,
                                        Win32.LR_DEFAULTSIZE | Win32.LR_SHARED);

        var theme = new Theme(new RegistryThemeSource());
        _menuIcons = new MenuIcons((int)Win32.GetDpiForWindow(ownerWindow), theme);

        _data = new Win32.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
            hWnd = ownerWindow,
            uID = IconId,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = Win32.WM_APP_TRAY,
            hIcon = hIcon,
            szTip = "Icônes du Bureau",
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
    }

    public event Action<int>? CommandChosen;

    /// <summary>
    /// Forwards a system settings change (theme flip) to the cached menu icons.
    /// <see cref="IconPalette.EnsureContrast"/> depends on the theme, so a light/dark
    /// switch changes what colour each icon is contrast-corrected to — without this, the
    /// bitmaps already cached under the old theme's colours would keep being reused.
    /// </summary>
    public void OnSettingsChanged() => _menuIcons.Invalidate();

    public void SetVisible(bool visible)
    {
        if (visible == _visible)
        {
            return;
        }

        // Record the new state only when the shell accepted it. Clearing _visible after
        // a failed NIM_DELETE would strand an icon this process can no longer address:
        // every later NIM_MODIFY and NIM_DELETE would be skipped by the guard above.
        if (Win32.Shell_NotifyIconW(visible ? Win32.NIM_ADD : Win32.NIM_DELETE, ref _data))
        {
            _visible = visible;
        }
    }

    /// <summary>Re-adds the icon after Explorer restarts.</summary>
    public void Readd()
    {
        if (_visible)
        {
            Win32.Shell_NotifyIconW(Win32.NIM_ADD, ref _data);
        }
    }

    public void UpdateTooltip(string text)
    {
        _data.szTip = text.Length > 127 ? text[..127] : text;
        if (_visible)
        {
            Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, ref _data);
        }
    }

    public void ShowMenu(TrayMenuState state)
    {
        IntPtr menu = Win32.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdToggle),
                              state.IconsVisible ? "Masquer les icônes" : "Afficher les icônes");
            SetItemIcon(menu, CmdToggle, LucideIcons.Icon.HideIcons, MenuIcons.HideIconsColor, 255);

            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);

            Win32.AppendMenuW(menu, Win32.MF_STRING | (state.HideTaskbar ? Win32.MF_CHECKED : 0),
                              new UIntPtr(CmdHideTaskbar), "Masquer aussi la barre des tâches");
            SetItemIcon(menu, CmdHideTaskbar, LucideIcons.Icon.Taskbar, MenuIcons.TaskbarColor,
                        state.HideTaskbar ? (byte)255 : IconPalette.InactiveAlpha);

            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdChangeHotkey),
                              $"Raccourci : {state.HotkeyLabel}…");
            SetItemIcon(menu, CmdChangeHotkey, LucideIcons.Icon.Keyboard, MenuIcons.KeyboardColor, 255);

            Win32.AppendMenuW(menu, Win32.MF_STRING | (state.AutostartEnabled ? Win32.MF_CHECKED : 0),
                              new UIntPtr(CmdAutostart), "Démarrage automatique");
            SetItemIcon(menu, CmdAutostart, LucideIcons.Icon.Autostart, MenuIcons.AutostartColor,
                        state.AutostartEnabled ? (byte)255 : IconPalette.InactiveAlpha);

            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);

            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdQuit), "Quitter");
            SetItemIcon(menu, CmdQuit, LucideIcons.Icon.Quit, MenuIcons.QuitColor, 255);

            Win32.GetCursorPos(out var cursor);
            // Required so the menu closes when the user clicks elsewhere.
            Win32.SetForegroundWindow(_data.hWnd);

            int chosen = Win32.TrackPopupMenu(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD,
                                              cursor.X, cursor.Y, 0, _data.hWnd, IntPtr.Zero);

            // Documented shell requirement, the counterpart to SetForegroundWindow above:
            // without it the menu can fail to dismiss when the user clicks away.
            Win32.PostMessageW(_data.hWnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (chosen != 0)
            {
                CommandChosen?.Invoke(chosen);
            }
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    /// <summary>
    /// Attaches the cached icon bitmap to a menu item via <c>MIIM_BITMAP</c>/
    /// <c>hbmpItem</c> — used for all five items, including the two checkable toggles.
    /// <c>hbmpItem</c> and the legacy <c>hbmpChecked</c>/<c>hbmpUnchecked</c> check-mark
    /// gutter cannot coexist and the latter ignores 32-bit alpha DIBs entirely, which is
    /// why the toggles used to show no icon at all; the caller still sets
    /// <c>MF_CHECKED</c> on those two items so the state stays correct for accessibility
    /// tools, and picks which bitmap (colour and alpha) to hand in here based on that same
    /// state, since no native check mark is drawn once an item has <c>hbmpItem</c>.
    /// </summary>
    private void SetItemIcon(IntPtr menu, int commandId, LucideIcons.Icon icon, uint color, byte alpha)
    {
        IntPtr bitmap = _menuIcons.Get(icon, _menuIcons.Contrast(color), alpha);
        if (bitmap == IntPtr.Zero)
        {
            return;
        }

        var info = new Win32.MENUITEMINFOW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.MENUITEMINFOW>(),
            fMask = Win32.MIIM_BITMAP,
            hbmpItem = bitmap,
        };

        Win32.SetMenuItemInfoW(menu, (uint)commandId, false, ref info);
    }

    public void Dispose()
    {
        SetVisible(false);
        _menuIcons.Dispose();
    }
}
