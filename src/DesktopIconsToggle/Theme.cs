using Microsoft.Win32;

namespace DesktopIconsToggle;

internal interface IThemeSource
{
    /// <summary>True for light, false for dark, null when the preference cannot be read.</summary>
    bool? IsLightTheme();

    /// <summary>
    /// True for light, false for dark, null when the preference cannot be read. This is
    /// <c>SystemUsesLightTheme</c> — the taskbar, the Start menu, the notification area —
    /// which Windows tracks independently of <see cref="IsLightTheme"/>'s
    /// <c>AppsUseLightTheme</c>. A dark taskbar with light apps (or the reverse) is a
    /// common, deliberate combination, not a misconfiguration.
    /// </summary>
    bool? IsLightSystemTheme();
}

/// <summary>
/// Reads the light/dark app and system theme preferences, both DWORDs (not strings), so
/// this cannot share <see cref="IRegistryValueStore"/>, which only ever returns strings.
/// </summary>
internal sealed class RegistryThemeSource : IThemeSource
{
    private const string SubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsValueName = "AppsUseLightTheme";
    private const string SystemValueName = "SystemUsesLightTheme";

    public bool? IsLightTheme() => ReadValue(AppsValueName);

    public bool? IsLightSystemTheme() => ReadValue(SystemValueName);

    private static bool? ReadValue(string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKeyPath, writable: false);
            return key?.GetValue(valueName) is int value ? value != 0 : null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}

internal sealed class Theme
{
    /// <summary>COLORREF values (0x00BBGGRR) for the dialog.</summary>
    public const uint LightBackground = 0x00FFFFFF;
    public const uint LightText = 0x00000000;
    public const uint LightSubtleText = 0x00646464;

    public const uint DarkBackground = 0x00202020;
    public const uint DarkText = 0x00FFFFFF;
    public const uint DarkSubtleText = 0x00A0A0A0;

    private readonly IThemeSource _source;

    public Theme(IThemeSource source)
    {
        _source = source;
        Refresh();
    }

    public bool IsDark { get; private set; }

    /// <summary>
    /// The system theme — taskbar, Start menu, notification area — as opposed to
    /// <see cref="IsDark"/>, which is the app theme. The two are set independently.
    /// </summary>
    public bool IsSystemDark { get; private set; }

    public uint Background { get; private set; }
    public uint Text { get; private set; }
    public uint SubtleText { get; private set; }

    /// <summary>Re-reads both preferences. Call when the app starts and when they may have changed.</summary>
    public void Refresh()
    {
        // An unreadable or missing preference means light, the safer default.
        bool isLight = _source.IsLightTheme() ?? true;
        IsDark = !isLight;

        bool isSystemLight = _source.IsLightSystemTheme() ?? true;
        IsSystemDark = !isSystemLight;

        Background = IsDark ? DarkBackground : LightBackground;
        Text = IsDark ? DarkText : LightText;
        SubtleText = IsDark ? DarkSubtleText : LightSubtleText;
    }
}
