namespace DesktopIconsToggle.Tests;

public class SystemFontTests
{
    // ---- Identity at 96 DPI ----

    [Theory]
    [InlineData(-9)]
    [InlineData(-12)]
    [InlineData(-16)]
    [InlineData(-20)]
    public void IdentityAt96DpiWithNoExtraPoints(int lfHeight)
    {
        Assert.Equal(lfHeight, SystemFont.ScaledHeight(lfHeight, 96, extraPoints: 0));
    }

    // ---- Correct values at 120/144/192 DPI ----

    [Theory]
    [InlineData(120, -15)]
    [InlineData(144, -18)]
    [InlineData(192, -24)]
    public void ScalesTheMessageFontHeightToTheTargetDpi(int dpi, int expected)
    {
        // -12 is a representative lfHeight (Segoe UI 9pt at 96 DPI).
        Assert.Equal(expected, SystemFont.ScaledHeight(-12, dpi, extraPoints: 0));
    }

    // ---- Negative lfHeight sign convention preserved ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void ResultStaysNegativeAcrossEveryDpi(int dpi)
    {
        // lfHeight is negative by Windows' character-height convention. The arithmetic
        // runs on the absolute value internally; the sign must still come back negative.
        Assert.True(SystemFont.ScaledHeight(-14, dpi, extraPoints: 0) < 0);
    }

    // ---- extraPoints offset applied ----

    [Theory]
    [InlineData(96, -20)]
    [InlineData(120, -25)]
    [InlineData(144, -30)]
    [InlineData(192, -40)]
    public void ExtraPointsAreConvertedToPixelsAt96DpiThenScaledWithTheBaseHeight(int dpi, int expected)
    {
        // Matches HotkeyCapture's actual combo-font call: base -12, extraPoints: 6.
        // 6pt = 8px at 96 DPI, so the 96-DPI magnitude is 12 + 8 = 20 before scaling.
        Assert.Equal(expected, SystemFont.ScaledHeight(-12, dpi, extraPoints: 6));
    }

    [Fact]
    public void ZeroExtraPointsLeavesTheBaseHeightUnchangedAt96Dpi()
    {
        Assert.Equal(SystemFont.ScaledHeight(-12, 96, extraPoints: 0),
                     SystemFont.ScaledHeight(-12, 96, extraPoints: 0));
        Assert.NotEqual(SystemFont.ScaledHeight(-12, 96, extraPoints: 0),
                        SystemFont.ScaledHeight(-12, 96, extraPoints: 6));
    }
}
