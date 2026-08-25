using System.Diagnostics;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

internal static class Program
{
    private const nuint TooltipTimerId = 1;

    private static AppWindow? _window;
    private static TrayIcon? _tray;
    private static DesktopWatcher? _watcher;
    private static Autostart? _autostart;
    private static Settings? _settings;
    private static TaskbarHider? _taskbar;
    private static HotKey? _hotKey;
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

        var settings = new Settings(new RegistryValueStore(Settings.SubKey));
        settings.Load();
        _settings = settings;

        _autostart = new Autostart(new RegistryValueStore(RegistryValueStore.RunSubKey), exePath);

        var taskbar = new TaskbarHider();
        _taskbar = taskbar;

        // Crash recovery, before capturing the original state: if a previous session was
        // killed with the taskbar auto-hidden by us, that auto-hide is ours, not the
        // user's. Undo it and clear the marker before CaptureOriginalState runs, or the
        // capture below would record our own leftover state as the user's original.
        if (settings.TaskbarHiddenByUs)
        {
            taskbar.SetAutoHide(false);
            settings.TaskbarHiddenByUs = false;
            settings.Save();
        }

        taskbar.CaptureOriginalState();

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
        watcher.IgnoreWindow = window.Handle;
        watcher.Changed += isDesktop => tray.SetVisible(isDesktop);
        bool watcherStarted = watcher.Start();

        if (!watcherStarted)
        {
            // Without the foreground hook the icon would never be added after its
            // initial state, so pin it visible: the menu, and Quitter, must stay reachable.
            tray.SetVisible(true);
        }

        var hotKey = new HotKey(window.Handle);
        _hotKey = hotKey;

        if (!hotKey.Register(settings.HotkeyModifiers, settings.HotkeyVirtualKey))
        {
            tray.UpdateTooltip("Icônes du Bureau — raccourci clavier indisponible");
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

        if (msg == Win32.WM_HOTKEY && (int)wParam == HotKey.Id)
        {
            ToggleAll();
            return true;
        }

        if (msg == Win32.WM_APP_TRAY)
        {
            int mouseMessage = (int)lParam;
            if (mouseMessage == Win32.WM_LBUTTONUP)
            {
                ToggleAll();
            }
            else if (mouseMessage == Win32.WM_RBUTTONUP)
            {
                _tray?.ShowMenu(new TrayMenuState(
                    IconToggler.AreIconsVisible(),
                    _settings!.HideTaskbar,
                    HotkeyFormatter.Format(_hotKey!.Modifiers, _hotKey.VirtualKey),
                    _autostart?.IsEnabled() ?? false));
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
                ToggleAll();
                break;

            case TrayIcon.CmdAutostart:
                _autostart?.Toggle();
                break;

            case TrayIcon.CmdHideTaskbar:
                _settings!.HideTaskbar = !_settings.HideTaskbar;
                _settings.Save();
                // Apply immediately so the checkbox matches what the user sees, rather
                // than only taking effect at the next toggle.
                ApplyTaskbarState(_settings.HideTaskbar && !IconToggler.AreIconsVisible());
                break;

            case TrayIcon.CmdChangeHotkey:
                ChangeHotkey();
                break;

            case TrayIcon.CmdQuit:
                Win32.PostQuitMessage(0);
                break;
        }
    }

    private static void ToggleAll()
    {
        // Read the target before toggling: Explorer takes a moment to reflect the
        // posted message, so asking again afterwards would give the stale answer.
        bool willHide = IconToggler.AreIconsVisible();

        if (!IconToggler.Toggle())
        {
            _tray?.UpdateTooltip("Icônes du Bureau — bureau indisponible");
            return;
        }

        if (_settings!.HideTaskbar)
        {
            ApplyTaskbarState(willHide);
        }

        // Explorer needs a moment before the window state reflects the posted toggle.
        // Sleeping here would stall the thread that services the low-level mouse hook,
        // and Windows silently unhooks a callback thread that overruns its timeout.
        if (_windowHandle != IntPtr.Zero)
        {
            Win32.SetTimer(_windowHandle, TooltipTimerId, 150, IntPtr.Zero);
        }
    }

    private static void ApplyTaskbarState(bool hidden)
    {
        // When restoring, go back to whatever the user had before we started —
        // someone who already used auto-hide must keep it.
        _taskbar!.SetAutoHide(hidden || _taskbar.OriginalAutoHide);

        // Only claim responsibility when we actually changed it from their original.
        _settings!.TaskbarHiddenByUs = hidden && !_taskbar.OriginalAutoHide;
        _settings.Save();
    }

    private static void ChangeHotkey()
    {
        uint previousModifiers = _hotKey!.Modifiers;
        uint previousVirtualKey = _hotKey.VirtualKey;

        // The user must be able to re-pick the combination they already hold.
        _hotKey.Unregister();

        if (HotkeyCapture.TryCapture(_windowHandle, out uint modifiers, out uint virtualKey)
            && _hotKey.Register(modifiers, virtualKey))
        {
            _settings!.HotkeyModifiers = modifiers;
            _settings.HotkeyVirtualKey = virtualKey;
            _settings.Save();
            RefreshTooltip();
            return;
        }

        // Cancelled, or the combination stopped being available between the dialog's
        // test and now: put the previous one back rather than leaving none at all.
        _hotKey.Register(previousModifiers, previousVirtualKey);
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

        _hotKey?.Dispose();
        _taskbar?.RestoreOriginal();

        if (_settings is not null)
        {
            _settings.TaskbarHiddenByUs = false;
            _settings.Save();
        }

        if (_windowHandle != IntPtr.Zero)
        {
            Win32.KillTimer(_windowHandle, TooltipTimerId);
        }

        _tray?.Dispose();
        _watcher?.Dispose();
        _window?.Dispose();
        _tray = null;
        _watcher = null;
        _window = null;
        _windowHandle = IntPtr.Zero;
        _hotKey = null;
        _taskbar = null;
    }
}
