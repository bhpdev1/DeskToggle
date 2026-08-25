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

    // Segoe MDL2 Assets codepoints (also present in Segoe Fluent Icons). Written as
    // numeric casts, not \u escapes, so the private-use-area glyphs cannot be mangled by
    // source file encoding round-trips.
    private const char GlyphEye       = (char)0xE890; // Afficher / Masquer les icônes
    private const char GlyphTaskbar   = (char)0xE7F4; // Masquer aussi la barre des tâches
    private const char GlyphKeyboard  = (char)0xE765; // Raccourci : …
    private const char GlyphAutostart = (char)0xE7E8; // Démarrage automatique
    private const char GlyphQuit      = (char)0xE711; // Quitter

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
    /// Forwards a system settings change (theme flip or accent colour change) to the
    /// cached menu icons. The glyph bitmaps carry real alpha and are cached per colour,
    /// so they no longer need this to stay correct after a theme flip; what does need it
    /// is the memoized Windows accent colour, which is otherwise only read once and would
    /// keep returning a value the user has since changed.
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
            SetItemIcon(menu, CmdToggle, GlyphEye, MenuIcons.NeutralColor, 255);

            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);

            Win32.AppendMenuW(menu, Win32.MF_STRING | (state.HideTaskbar ? Win32.MF_CHECKED : 0),
                              new UIntPtr(CmdHideTaskbar), "Masquer aussi la barre des tâches");
            SetItemCheckIcons(menu, CmdHideTaskbar, GlyphTaskbar);

            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdChangeHotkey),
                              $"Raccourci : {state.HotkeyLabel}…");
            SetItemIcon(menu, CmdChangeHotkey, GlyphKeyboard, MenuIcons.NeutralColor, 255);

            Win32.AppendMenuW(menu, Win32.MF_STRING | (state.AutostartEnabled ? Win32.MF_CHECKED : 0),
                              new UIntPtr(CmdAutostart), "Démarrage automatique");
            SetItemCheckIcons(menu, CmdAutostart, GlyphAutostart);

            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);

            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdQuit), "Quitter");
            SetItemIcon(menu, CmdQuit, GlyphQuit, _menuIcons.Contrast(MenuIcons.RedColor), 255);

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
    /// Attaches the cached glyph bitmap to a non-checkable menu item via
    /// <c>MIIM_BITMAP</c>/<c>hbmpItem</c>. A no-op when the icon font is unavailable
    /// (<see cref="MenuIcons.Get"/> returns <see cref="IntPtr.Zero"/>) so the item
    /// degrades to text-only rather than showing an empty gutter.
    /// </summary>
    private void SetItemIcon(IntPtr menu, int commandId, char glyph, uint color, byte alpha)
    {
        IntPtr bitmap = _menuIcons.Get(glyph, color, alpha);
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

    /// <summary>
    /// Attaches both state bitmaps to a checkable menu item via
    /// <c>MIIM_CHECKMARKS</c>/<c>hbmpUnchecked</c>/<c>hbmpChecked</c>, so Windows itself
    /// picks between them according to the item's <c>MF_CHECKED</c> state (set by the
    /// caller in the <c>AppendMenuW</c> flags) instead of drawing a native check mark —
    /// <c>hbmpItem</c> and the check-mark gutter cannot coexist, so this is the only way
    /// to keep the item's checked/unchecked state visible once it also has an icon. The
    /// unchecked state is the neutral colour dimmed to <see cref="IconPalette.InactiveAlpha"/>
    /// rather than a duller hue, since an accent colour can already be too muted to read as
    /// "on" by itself (see <see cref="IconPalette"/>); the checked state is the accent
    /// colour, contrast-corrected, at full alpha. A no-op when the icon font is
    /// unavailable, leaving the native check mark in place.
    /// </summary>
    private void SetItemCheckIcons(IntPtr menu, int commandId, char glyph)
    {
        IntPtr unchecked_ = _menuIcons.Get(glyph, MenuIcons.NeutralColor, IconPalette.InactiveAlpha);
        IntPtr checked_ = _menuIcons.Get(glyph, _menuIcons.Contrast(_menuIcons.AccentColor()), 255);
        if (unchecked_ == IntPtr.Zero || checked_ == IntPtr.Zero)
        {
            return;
        }

        var info = new Win32.MENUITEMINFOW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.MENUITEMINFOW>(),
            fMask = Win32.MIIM_CHECKMARKS,
            hbmpUnchecked = unchecked_,
            hbmpChecked = checked_,
        };

        Win32.SetMenuItemInfoW(menu, (uint)commandId, false, ref info);
    }

    public void Dispose()
    {
        SetVisible(false);
        _menuIcons.Dispose();
    }
}
