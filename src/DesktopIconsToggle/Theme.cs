using Microsoft.Win32;

namespace DesktopIconsToggle;

internal interface IThemeSource
{
    /// <summary>True for light, false for dark, null when the preference cannot be read.</summary>
    bool? IsLightTheme();
}

/// <summary>
/// Reads the light/dark app theme preference, a DWORD (not a string), so it cannot
/// share <see cref="IRegistryValueStore"/>, which only ever returns strings.
/// </summary>
internal sealed class RegistryThemeSource : IThemeSource
{
    private const string SubKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    public bool? IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKeyPath, writable: false);
            return key?.GetValue(ValueName) is int value ? value != 0 : null;
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
    public uint Background { get; private set; }
    public uint Text { get; private set; }
    public uint SubtleText { get; private set; }

    /// <summary>Re-reads the preference. Call when the app starts and when it may have changed.</summary>
    public void Refresh()
    {
        // An unreadable or missing preference means light, the safer default.
        bool isLight = _source.IsLightTheme() ?? true;
        IsDark = !isLight;

        Background = IsDark ? DarkBackground : LightBackground;
        Text = IsDark ? DarkText : LightText;
        SubtleText = IsDark ? DarkSubtleText : LightSubtleText;
    }
}
