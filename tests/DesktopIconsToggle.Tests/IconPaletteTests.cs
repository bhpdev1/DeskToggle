namespace DesktopIconsToggle.Tests;

public class IconPaletteTests
{
    [Fact]
    public void LuminanceOfPureBlackIsZero()
    {
        Assert.Equal(0, IconPalette.Luminance(0x00000000u));
    }

    [Fact]
    public void LuminanceOfPureWhiteIsTwoFiftyFive()
    {
        Assert.Equal(255, IconPalette.Luminance(0x00FFFFFFu));
    }

    [Fact]
    public void LuminanceOfPureRedIsFiftyFour()
    {
        // COLORREF is 0x00bbggrr: red lives in the low byte.
        Assert.Equal(54, IconPalette.Luminance(0x000000FFu));
    }

    [Fact]
    public void LuminanceOfPureGreenIsOneEightyTwo()
    {
        Assert.Equal(182, IconPalette.Luminance(0x0000FF00u));
    }

    [Fact]
    public void LuminanceOfPureBlueIsEighteen()
    {
        // COLORREF is 0x00bbggrr: blue lives in the high byte.
        Assert.Equal(18, IconPalette.Luminance(0x00FF0000u));
    }

    [Fact]
    public void MeasuredAccentOnDarkBackgroundReachesTargetAndKeepsItsHue()
    {
        // RGB(68, 77, 89) as a COLORREF (0x00bbggrr): the user's measured, deliberately
        // desaturated slate accent, dim enough on its own to fail contrast against a dark
        // menu background.
        const uint accent = 0x00594D44u;

        uint result = IconPalette.EnsureContrast(accent, isDark: true);

        Assert.True(IconPalette.Luminance(result) >= IconPalette.DarkTargetLuminance);

        int r = (int)(result & 0xFF);
        int g = (int)((result >> 8) & 0xFF);
        int b = (int)((result >> 16) & 0xFF);
        // Still recognisably the same slate hue: blue the largest channel, then green,
        // then red, exactly as in the original.
        Assert.True(b > g);
        Assert.True(g > r);

        // Locks in the exact hand-worked result: RGB(146, 151, 158).
        Assert.Equal(0x009E9792u, result);
    }

    [Fact]
    public void AlreadyBrightColourOnDarkBackgroundIsUnchanged()
    {
        // A mid-light grey (COLORREF byte order does not matter when r=g=b): luminance
        // 200, already past DarkTargetLuminance (150).
        const uint lightGrey = 0x00C8C8C8u;

        Assert.Equal(lightGrey, IconPalette.EnsureContrast(lightGrey, isDark: true));
    }

    [Fact]
    public void AlreadyDarkColourOnLightBackgroundIsUnchanged()
    {
        // A dark grey: luminance 50, already under LightTargetLuminance (110).
        const uint darkGrey = 0x00323232u;

        Assert.Equal(darkGrey, IconPalette.EnsureContrast(darkGrey, isDark: false));
    }

    [Fact]
    public void BrightColourOnLightBackgroundIsDarkenedToAtMostTarget()
    {
        // Same mid-light grey as above (luminance 200), but now against a light
        // background, where it reads as too bright rather than too dim.
        const uint lightGrey = 0x00C8C8C8u;

        uint result = IconPalette.EnsureContrast(lightGrey, isDark: false);

        Assert.True(IconPalette.Luminance(result) <= IconPalette.LightTargetLuminance);
        Assert.NotEqual(lightGrey, result);
    }

    [Fact]
    public void WhiteOnDarkAndBlackOnLightAreBothNoOps()
    {
        Assert.Equal(0x00FFFFFFu, IconPalette.EnsureContrast(0x00FFFFFFu, isDark: true));
        Assert.Equal(0x00000000u, IconPalette.EnsureContrast(0x00000000u, isDark: false));
    }
}
