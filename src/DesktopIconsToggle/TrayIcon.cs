using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

internal sealed class TrayIcon : IDisposable
{
    public const int CmdToggle    = 1;
    public const int CmdAutostart = 2;
    public const int CmdQuit      = 3;

    private const uint IconId = 1;

    private Win32.NOTIFYICONDATAW _data;
    private bool _visible;

    public TrayIcon(IntPtr ownerWindow)
    {
        IntPtr hIcon = Win32.LoadImageW(Win32.GetModuleHandleW(null),
                                        new IntPtr(Win32.IDI_APPLICATION),
                                        Win32.IMAGE_ICON, 0, 0,
                                        Win32.LR_DEFAULTSIZE | Win32.LR_SHARED);

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

    public void SetVisible(bool visible)
    {
        if (visible == _visible)
        {
            return;
        }

        bool ok = Win32.Shell_NotifyIconW(visible ? Win32.NIM_ADD : Win32.NIM_DELETE, ref _data);
        _visible = visible && ok;
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

    public void ShowMenu(bool iconsVisible, bool autostartEnabled)
    {
        IntPtr menu = Win32.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdToggle),
                              iconsVisible ? "Masquer les icônes" : "Afficher les icônes");
            Win32.AppendMenuW(menu, Win32.MF_STRING | (autostartEnabled ? Win32.MF_CHECKED : 0),
                              new UIntPtr(CmdAutostart), "Démarrage automatique");
            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
            Win32.AppendMenuW(menu, Win32.MF_STRING, new UIntPtr(CmdQuit), "Quitter");

            Win32.GetCursorPos(out var cursor);
            // Required so the menu closes when the user clicks elsewhere.
            Win32.SetForegroundWindow(_data.hWnd);

            int chosen = Win32.TrackPopupMenu(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD,
                                              cursor.X, cursor.Y, 0, _data.hWnd, IntPtr.Zero);
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

    public void Dispose() => SetVisible(false);
}
