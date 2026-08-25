using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Owns a single global hotkey registration on the app's top-level window. It must be a
/// real top-level window, not a message-only one (HWND_MESSAGE): RegisterHotKey does not
/// work with message-only windows.
/// No threads, no timers: WM_HOTKEY arrives on the window's own message loop.
/// </summary>
internal sealed class HotKey : IDisposable
{
    public const int Id = 1;

    private readonly IntPtr _window;

    public HotKey(IntPtr window)
    {
        _window = window;
    }

    public bool IsRegistered { get; private set; }
    public uint Modifiers { get; private set; }
    public uint VirtualKey { get; private set; }

    /// <summary>
    /// Registers the combination, replacing any current one. Returns false and
    /// leaves the previous registration in place when Windows refuses the new
    /// combination — typically because the system or another application already
    /// owns it.
    /// </summary>
    public bool Register(uint modifiers, uint virtualKey)
    {
        bool hadPrevious = IsRegistered;
        uint previousModifiers = Modifiers;
        uint previousVirtualKey = VirtualKey;

        Unregister();

        if (Win32.RegisterHotKey(_window, Id, modifiers | Win32.MOD_NOREPEAT, virtualKey))
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            IsRegistered = true;
            return true;
        }

        // The new combination was refused. Put the previous one back rather than
        // silently leaving the user with no hotkey at all.
        if (hadPrevious)
        {
            IsRegistered = Win32.RegisterHotKey(_window, Id, previousModifiers | Win32.MOD_NOREPEAT, previousVirtualKey);
        }

        return false;
    }

    public void Unregister()
    {
        if (IsRegistered)
        {
            Win32.UnregisterHotKey(_window, Id);
            IsRegistered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
    }
}
