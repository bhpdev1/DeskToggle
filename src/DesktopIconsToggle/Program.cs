using System.Diagnostics;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

internal static class Program
{
    private const nuint TooltipTimerId = 1;

    private static AppWindow? _window;
    private static TrayIcon? _tray;
    private static DesktopWatcher? _watcher;
    private static MouseHook? _mouseHook;
    private static Autostart? _autostart;
    private static uint _taskbarCreatedMessage;
    private static IntPtr _windowHandle;
    private static int _shutdownStarted;

    private static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\DesktopIconsToggle", out bool isNew);
        if (!isNew)
        {
            return 0;
        }

        string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
        _autostart = new Autostart(new RunKeyStore(), exePath);

        _taskbarCreatedMessage = Win32.RegisterWindowMessageW("TaskbarCreated");

        var window = new AppWindow();
        _window = window;
        _windowHandle = window.Handle;
        window.MessageReceived += OnMessage;

        // Locals, not the nullable fields: a lambda capturing a nullable field
        // loses its flow analysis and trips CS8602 under TreatWarningsAsErrors.
        var tray = new TrayIcon(window.Handle);
        _tray = tray;
        tray.CommandChosen += OnCommand;

        var watcher = new DesktopWatcher();
        _watcher = watcher;
        watcher.Changed += isDesktop => tray.SetVisible(isDesktop);
        bool watcherStarted = watcher.Start();

        if (!watcherStarted)
        {
            // Without the foreground hook the icon would never be added after its
            // initial state, so pin it visible: the menu, and Quitter, must stay reachable.
            tray.SetVisible(true);
        }

        var mouseHook = new MouseHook(window.Handle);
        _mouseHook = mouseHook;
        if (!mouseHook.Install())
        {
            tray.UpdateTooltip("Icônes du Bureau — raccourci souris indisponible");
        }
        else
        {
            RefreshTooltip();
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

        window.RunMessageLoop();

        Shutdown();
        return 0;
    }

    private static bool OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_TIMER && (nuint)wParam == TooltipTimerId)
        {
            Win32.KillTimer(_windowHandle, TooltipTimerId);
            RefreshTooltip();
            return true;
        }

        if (msg == Win32.WM_APP_TOGGLE)
        {
            ToggleIcons();
            return true;
        }

        if (msg == Win32.WM_APP_TRAY)
        {
            int mouseMessage = (int)lParam;
            if (mouseMessage == Win32.WM_LBUTTONUP)
            {
                ToggleIcons();
            }
            else if (mouseMessage == Win32.WM_RBUTTONUP)
            {
                _tray?.ShowMenu(IconToggler.AreIconsVisible(), _autostart?.IsEnabled() ?? false);
            }

            return true;
        }

        if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            _tray?.Readd();
            return true;
        }

        // wParam is FALSE when a previously announced session end was cancelled;
        // the app must keep running in that case.
        if (msg == Win32.WM_ENDSESSION && wParam != IntPtr.Zero)
        {
            // Restore synchronously: the system may terminate the process at any point
            // after this returns, so deferring the restore to the message loop is not safe.
            Shutdown();
            return true;
        }

        return false;
    }

    private static void OnCommand(int command)
    {
        switch (command)
        {
            case TrayIcon.CmdToggle:
                ToggleIcons();
                break;

            case TrayIcon.CmdAutostart:
                _autostart?.Toggle();
                break;

            case TrayIcon.CmdQuit:
                Win32.PostQuitMessage(0);
                break;
        }
    }

    private static void ToggleIcons()
    {
        if (!IconToggler.Toggle())
        {
            _tray?.UpdateTooltip("Icônes du Bureau — bureau indisponible");
            return;
        }

        // Explorer needs a moment before the window state reflects the posted toggle.
        // Sleeping here would stall the thread that services the low-level mouse hook,
        // and Windows silently unhooks a callback thread that overruns its timeout.
        if (_windowHandle != IntPtr.Zero)
        {
            Win32.SetTimer(_windowHandle, TooltipTimerId, 150, IntPtr.Zero);
        }
    }

    private static void RefreshTooltip()
        => _tray?.UpdateTooltip(IconToggler.AreIconsVisible()
            ? "Icônes du Bureau — affichées"
            : "Icônes du Bureau — masquées");

    private static void Shutdown()
    {
        // Both the message loop's return and the ProcessExit handler call this.
        // Without the guard, EnsureVisible runs twice milliseconds apart — and because
        // Explorer needs a moment to reflect a posted toggle, the second call would
        // still read "hidden" and toggle again, re-hiding what the first call restored.
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        IconToggler.EnsureVisible();   // never leave the user without icons
        if (_windowHandle != IntPtr.Zero)
        {
            Win32.KillTimer(_windowHandle, TooltipTimerId);
        }

        _tray?.Dispose();
        _mouseHook?.Dispose();
        _watcher?.Dispose();
        _window?.Dispose();
        _tray = null;
        _mouseHook = null;
        _watcher = null;
        _window = null;
        _windowHandle = IntPtr.Zero;
    }
}
