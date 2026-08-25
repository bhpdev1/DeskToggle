namespace DesktopIconsToggle;

/// <summary>
/// Adjusts icon colours so they stay legible against the menu background. Accent colours
/// are user-chosen and often deliberately muted, so painting one unmodified can leave an
/// enabled option looking dimmer than a disabled one — the opposite of the intended signal.
/// One rule is applied uniformly: lift or darken the colour until it has enough contrast,
/// then paint it. The inactive state is signalled with alpha instead of hue (see
/// <see cref="InactiveAlpha"/>), so this type only ever has to move a colour toward one
/// extreme or the other, never toward grey.
/// </summary>
internal static class IconPalette
{
    /// <summary>Alpha applied to the neutral, inactive icon. 0-255.</summary>
    public const byte InactiveAlpha = 115; // ~45 %

    /// <summary>Luminance an icon must reach on a dark background.</summary>
    public const int DarkTargetLuminance = 150;

    /// <summary>Luminance an icon must not exceed on a light background.</summary>
    public const int LightTargetLuminance = 110;

    /// <summary>Perceptual luminance on a 0-255 scale, from an 0x00BBGGRR COLORREF.</summary>
    public static int Luminance(uint colorRef)
    {
        int r = (int)(colorRef & 0xFF);
        int g = (int)((colorRef >> 8) & 0xFF);
        int b = (int)((colorRef >> 16) & 0xFF);
        return (2126 * r + 7152 * g + 722 * b) / 10000;
    }

    /// <summary>
    /// Blends the colour toward white on a dark background, or toward black on a light
    /// one, only as far as needed to reach the minimum contrast. Returns it unchanged when
    /// it already contrasts enough.
    /// </summary>
    public static uint EnsureContrast(uint colorRef, bool isDark)
    {
        int r = (int)(colorRef & 0xFF);
        int g = (int)((colorRef >> 8) & 0xFF);
        int b = (int)((colorRef >> 16) & 0xFF);
        int l = Luminance(colorRef);

        if (isDark)
        {
            if (l >= DarkTargetLuminance)
            {
                return colorRef;
            }

            int span = 255 - l; // always > 0 here: l < DarkTargetLuminance <= 255
            // Ceiling, not truncating, division: the target is a floor the caller has
            // promised to reach, and Luminance() truncates too, so rounding the blend
            // fraction down here could leave the result's own (separately truncated)
            // luminance a shade under the target it was supposed to hit.
            int t = ((DarkTargetLuminance - l) * 255 + span - 1) / span;
            if (t > 255)
            {
                t = 255;
            }

            r += (255 - r) * t / 255;
            g += (255 - g) * t / 255;
            b += (255 - b) * t / 255;
        }
        else
        {
            if (l <= LightTargetLuminance)
            {
                return colorRef;
            }

            int span = l; // always > 0 here: l > LightTargetLuminance >= 0
            int t = ((l - LightTargetLuminance) * 255 + span - 1) / span;
            if (t > 255)
            {
                t = 255;
            }

            r -= r * t / 255;
            g -= g * t / 255;
            b -= b * t / 255;
        }

        return ((uint)b << 16) | ((uint)g << 8) | (uint)r;
    }
}
