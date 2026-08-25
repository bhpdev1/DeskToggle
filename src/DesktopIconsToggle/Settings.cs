using System.Globalization;

namespace DesktopIconsToggle;

/// <summary>Persists user settings under a dedicated registry key. Load() is total: any missing,
/// empty, or unparseable value falls back to its own default without affecting the others.</summary>
internal sealed class Settings
{
    public const string SubKey = @"Software\DesktopIconsToggle";

    public const uint DefaultHotkeyModifiers = 0x0008; // MOD_WIN
    public const uint DefaultHotkeyVirtualKey = 0x1B;  // VK_ESCAPE
    public const bool DefaultHideTaskbar = false;
    public const bool DefaultTaskbarHiddenByUs = false;
    public const bool DefaultIconsHiddenByUs = false;

    private const string NameHotkeyModifiers = "HotkeyModifiers";
    private const string NameHotkeyVirtualKey = "HotkeyVirtualKey";
    private const string NameHideTaskbar = "HideTaskbar";
    private const string NameTaskbarHiddenByUs = "TaskbarHiddenByUs";
    private const string NameIconsHiddenByUs = "IconsHiddenByUs";

    private readonly IRegistryValueStore _store;

    public Settings(IRegistryValueStore store)
    {
        _store = store;
    }

    public uint HotkeyModifiers { get; set; } = DefaultHotkeyModifiers;
    public uint HotkeyVirtualKey { get; set; } = DefaultHotkeyVirtualKey;
    public bool HideTaskbar { get; set; } = DefaultHideTaskbar;
    public bool TaskbarHiddenByUs { get; set; } = DefaultTaskbarHiddenByUs;

    /// <summary>
    /// Records that this app hid the desktop icons. The app clears it on every clean exit,
    /// so finding it set at startup means the previous session was killed without restoring
    /// them — Explorer persists the hidden state, so nothing else would ever bring them back.
    /// </summary>
    public bool IconsHiddenByUs { get; set; } = DefaultIconsHiddenByUs;

    public void Load()
    {
        HotkeyModifiers = ReadUInt(NameHotkeyModifiers, DefaultHotkeyModifiers);
        HotkeyVirtualKey = ReadUInt(NameHotkeyVirtualKey, DefaultHotkeyVirtualKey);
        HideTaskbar = ReadBool(NameHideTaskbar, DefaultHideTaskbar);
        TaskbarHiddenByUs = ReadBool(NameTaskbarHiddenByUs, DefaultTaskbarHiddenByUs);
        IconsHiddenByUs = ReadBool(NameIconsHiddenByUs, DefaultIconsHiddenByUs);
    }

    public void Save()
    {
        _store.Write(NameHotkeyModifiers, HotkeyModifiers.ToString(CultureInfo.InvariantCulture));
        _store.Write(NameHotkeyVirtualKey, HotkeyVirtualKey.ToString(CultureInfo.InvariantCulture));
        _store.Write(NameHideTaskbar, HideTaskbar ? "1" : "0");
        _store.Write(NameTaskbarHiddenByUs, TaskbarHiddenByUs ? "1" : "0");
        _store.Write(NameIconsHiddenByUs, IconsHiddenByUs ? "1" : "0");
    }

    private uint ReadUInt(string name, uint fallback)
    {
        string? raw = _store.Read(name);
        if (string.IsNullOrEmpty(raw))
        {
            return fallback;
        }

        return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value)
            ? value
            : fallback;
    }

    private bool ReadBool(string name, bool fallback)
    {
        string? raw = _store.Read(name);
        if (string.IsNullOrEmpty(raw))
        {
            return fallback;
        }

        return raw switch
        {
            "1" => true,
            "0" => false,
            _ => fallback,
        };
    }
}
