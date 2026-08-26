namespace DesktopIconsToggle.Tests;

public class HotkeyFormatterTests
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    [Fact]
    public void NoModifiersReturnsJustTheKeyName()
    {
        Assert.Equal("D", HotkeyFormatter.Format(0, 0x44));
    }

    [Fact]
    public void LetterKey()
    {
        Assert.Equal("A", HotkeyFormatter.Format(0, 0x41));
    }

    [Fact]
    public void DigitKey()
    {
        Assert.Equal("5", HotkeyFormatter.Format(0, 0x35));
    }

    [Fact]
    public void FunctionKey()
    {
        Assert.Equal("F1", HotkeyFormatter.Format(0, 0x70));
        Assert.Equal("F24", HotkeyFormatter.Format(0, 0x87));
    }

    [Fact]
    public void EscapeKeyIsAccentedFrench()
    {
        Assert.Equal("Échap", HotkeyFormatter.Format(0, 0x1B));
    }

    [Fact]
    public void EnterKeyIsAccentedFrench()
    {
        Assert.Equal("Entrée", HotkeyFormatter.Format(0, 0x0D));
    }

    [Fact]
    public void PageUpKeyHasAccentAndPeriod()
    {
        Assert.Equal("Page préc.", HotkeyFormatter.Format(0, 0x21));
    }

    [Fact]
    public void ArrowKey()
    {
        Assert.Equal("Gauche", HotkeyFormatter.Format(0, 0x25));
        Assert.Equal("Haut", HotkeyFormatter.Format(0, 0x26));
        Assert.Equal("Droite", HotkeyFormatter.Format(0, 0x27));
        Assert.Equal("Bas", HotkeyFormatter.Format(0, 0x28));
    }

    [Fact]
    public void UnknownKeyFallsBackToHex()
    {
        Assert.Equal("Touche 0xC0", HotkeyFormatter.Format(0, 0xC0));
    }

    [Fact]
    public void UnknownKeyHexIsUppercaseTwoDigits()
    {
        Assert.Equal("Touche 0x05", HotkeyFormatter.Format(0, 0x05));
    }

    [Fact]
    public void AltModifierAlone()
    {
        Assert.Equal("Alt+D", HotkeyFormatter.Format(ModAlt, 0x44));
    }

    [Fact]
    public void ControlModifierAlone()
    {
        Assert.Equal("Ctrl+D", HotkeyFormatter.Format(ModControl, 0x44));
    }

    [Fact]
    public void ShiftModifierAlone()
    {
        Assert.Equal("Maj+D", HotkeyFormatter.Format(ModShift, 0x44));
    }

    [Fact]
    public void WinModifierAlone()
    {
        Assert.Equal("Win+D", HotkeyFormatter.Format(ModWin, 0x44));
    }

    [Fact]
    public void WinAndShiftGiveWinShiftD()
    {
        Assert.Equal("Win+Maj+D", HotkeyFormatter.Format(ModWin | ModShift, 0x44));
    }

    [Fact]
    public void AllFourModifiersAreOrderedWinCtrlAltShift()
    {
        Assert.Equal("Win+Ctrl+Alt+Maj+Échap", HotkeyFormatter.Format(ModWin | ModControl | ModAlt | ModShift, 0x1B));
    }

    [Fact]
    public void ModifierOrderIsFixedRegardlessOfInputBitOrder()
    {
        // Shift and Alt set, but flip the "obvious" bit ordering when combining.
        Assert.Equal("Alt+Maj+F5", HotkeyFormatter.Format(ModShift | ModAlt, 0x74));
    }

    // ---- IsCommonlyUsedByApps ----

    public static IEnumerable<object[]> CommonAppKeys =>
    [
        [0x09u], // Tab
        [0x57u], // W
        [0x53u], // S
        [0x46u], // F
        [0x43u], // C
        [0x56u], // V
        [0x58u], // X
        [0x5Au], // Z
        [0x41u], // A
        [0x4Eu], // N
        [0x54u], // T
        [0x50u], // P
    ];

    [Theory]
    [MemberData(nameof(CommonAppKeys))]
    public void CtrlAloneWithAListedKeyIsCommonlyUsedByApps(uint virtualKey)
    {
        Assert.True(HotkeyFormatter.IsCommonlyUsedByApps(ModControl, virtualKey));
    }

    [Theory]
    [MemberData(nameof(CommonAppKeys))]
    public void CtrlWinWithAListedKeyIsNotCommonlyUsedByApps(uint virtualKey)
    {
        Assert.False(HotkeyFormatter.IsCommonlyUsedByApps(ModControl | ModWin, virtualKey));
    }

    [Theory]
    [MemberData(nameof(CommonAppKeys))]
    public void AListedKeyWithoutCtrlIsNotCommonlyUsedByApps(uint virtualKey)
    {
        Assert.False(HotkeyFormatter.IsCommonlyUsedByApps(0, virtualKey));
        Assert.False(HotkeyFormatter.IsCommonlyUsedByApps(ModAlt | ModShift, virtualKey));
    }

    [Fact]
    public void CtrlWithAKeyNotOnTheListIsNotCommonlyUsedByApps()
    {
        Assert.False(HotkeyFormatter.IsCommonlyUsedByApps(ModControl, 0x44)); // D
    }

    [Fact]
    public void CtrlAltDTheAppsOwnDefaultIsNotCommonlyUsedByApps()
    {
        Assert.False(HotkeyFormatter.IsCommonlyUsedByApps(ModControl | ModAlt, 0x44)); // D
    }
}
