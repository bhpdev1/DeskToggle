namespace DesktopIconsToggle.Tests;

public class ThemeTests
{
    private sealed class FakeThemeSource : IThemeSource
    {
        public bool? Value;

        public bool? IsLightTheme() => Value;
    }

    [Fact]
    public void LightSourceGivesTheLightTripleAndIsNotDark()
    {
        var theme = new Theme(new FakeThemeSource { Value = true });

        Assert.False(theme.IsDark);
        Assert.Equal(Theme.LightBackground, theme.Background);
        Assert.Equal(Theme.LightText, theme.Text);
        Assert.Equal(Theme.LightSubtleText, theme.SubtleText);
    }

    [Fact]
    public void DarkSourceGivesTheDarkTripleAndIsDark()
    {
        var theme = new Theme(new FakeThemeSource { Value = false });

        Assert.True(theme.IsDark);
        Assert.Equal(Theme.DarkBackground, theme.Background);
        Assert.Equal(Theme.DarkText, theme.Text);
        Assert.Equal(Theme.DarkSubtleText, theme.SubtleText);
    }

    [Fact]
    public void UnreadablePreferenceFallsBackToLight()
    {
        var theme = new Theme(new FakeThemeSource { Value = null });

        Assert.False(theme.IsDark);
        Assert.Equal(Theme.LightBackground, theme.Background);
        Assert.Equal(Theme.LightText, theme.Text);
        Assert.Equal(Theme.LightSubtleText, theme.SubtleText);
    }

    [Fact]
    public void RefreshPicksUpAChangedPreference()
    {
        var source = new FakeThemeSource { Value = true };
        var theme = new Theme(source);
        Assert.False(theme.IsDark);

        source.Value = false;
        theme.Refresh();

        Assert.True(theme.IsDark);
        Assert.Equal(Theme.DarkBackground, theme.Background);
        Assert.Equal(Theme.DarkText, theme.Text);
        Assert.Equal(Theme.DarkSubtleText, theme.SubtleText);
    }

    [Fact]
    public void RefreshCanFallBackToLightAfterBeingDark()
    {
        var source = new FakeThemeSource { Value = false };
        var theme = new Theme(source);
        Assert.True(theme.IsDark);

        source.Value = null;
        theme.Refresh();

        Assert.False(theme.IsDark);
        Assert.Equal(Theme.LightBackground, theme.Background);
    }

    [Fact]
    public void ColourConstantsMatchTheDocumentedColorrefValues()
    {
        Assert.Equal(0x00FFFFFFu, Theme.LightBackground);
        Assert.Equal(0x00000000u, Theme.LightText);
        Assert.Equal(0x00646464u, Theme.LightSubtleText);

        Assert.Equal(0x00202020u, Theme.DarkBackground);
        Assert.Equal(0x00FFFFFFu, Theme.DarkText);
        Assert.Equal(0x00A0A0A0u, Theme.DarkSubtleText);
    }
}
