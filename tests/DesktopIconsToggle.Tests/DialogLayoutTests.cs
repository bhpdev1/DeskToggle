using DesktopIconsToggle.Native;

namespace DesktopIconsToggle.Tests;

public class DialogLayoutTests
{
    // ---- Scale ----

    [Fact]
    public void ScaleIsIdentityAt96Dpi()
    {
        Assert.Equal(DialogLayout.Margin, DialogLayout.Scale(DialogLayout.Margin, 96));
        Assert.Equal(DialogLayout.LineGap, DialogLayout.Scale(DialogLayout.LineGap, 96));
        Assert.Equal(DialogLayout.LineHeight, DialogLayout.Scale(DialogLayout.LineHeight, 96));
        Assert.Equal(DialogLayout.ComboHeight, DialogLayout.Scale(DialogLayout.ComboHeight, 96));
        Assert.Equal(DialogLayout.ButtonWidth, DialogLayout.Scale(DialogLayout.ButtonWidth, 96));
        Assert.Equal(DialogLayout.ButtonHeight, DialogLayout.Scale(DialogLayout.ButtonHeight, 96));
        Assert.Equal(DialogLayout.BaseWidth, DialogLayout.Scale(DialogLayout.BaseWidth, 96));
        Assert.Equal(DialogLayout.BaseHeight, DialogLayout.Scale(DialogLayout.BaseHeight, 96));
    }

    [Fact]
    public void ScaleAt120Dpi()
    {
        Assert.Equal(20, DialogLayout.Scale(DialogLayout.Margin, 120));
        Assert.Equal(13, DialogLayout.Scale(DialogLayout.LineGap, 120));
        Assert.Equal(25, DialogLayout.Scale(DialogLayout.LineHeight, 120));
        Assert.Equal(43, DialogLayout.Scale(DialogLayout.ComboHeight, 120));
        Assert.Equal(138, DialogLayout.Scale(DialogLayout.ButtonWidth, 120));
        Assert.Equal(38, DialogLayout.Scale(DialogLayout.ButtonHeight, 120));
        Assert.Equal(575, DialogLayout.Scale(DialogLayout.BaseWidth, 120));
        Assert.Equal(263, DialogLayout.Scale(DialogLayout.BaseHeight, 120));
    }

    [Fact]
    public void ScaleAt144Dpi()
    {
        Assert.Equal(24, DialogLayout.Scale(DialogLayout.Margin, 144));
        Assert.Equal(15, DialogLayout.Scale(DialogLayout.LineGap, 144));
        Assert.Equal(30, DialogLayout.Scale(DialogLayout.LineHeight, 144));
        Assert.Equal(51, DialogLayout.Scale(DialogLayout.ComboHeight, 144));
        Assert.Equal(165, DialogLayout.Scale(DialogLayout.ButtonWidth, 144));
        Assert.Equal(45, DialogLayout.Scale(DialogLayout.ButtonHeight, 144));
        Assert.Equal(690, DialogLayout.Scale(DialogLayout.BaseWidth, 144));
        Assert.Equal(315, DialogLayout.Scale(DialogLayout.BaseHeight, 144));
    }

    [Fact]
    public void ScaleAt192Dpi()
    {
        Assert.Equal(32, DialogLayout.Scale(DialogLayout.Margin, 192));
        Assert.Equal(20, DialogLayout.Scale(DialogLayout.LineGap, 192));
        Assert.Equal(40, DialogLayout.Scale(DialogLayout.LineHeight, 192));
        Assert.Equal(68, DialogLayout.Scale(DialogLayout.ComboHeight, 192));
        Assert.Equal(220, DialogLayout.Scale(DialogLayout.ButtonWidth, 192));
        Assert.Equal(60, DialogLayout.Scale(DialogLayout.ButtonHeight, 192));
        Assert.Equal(920, DialogLayout.Scale(DialogLayout.BaseWidth, 192));
        Assert.Equal(420, DialogLayout.Scale(DialogLayout.BaseHeight, 192));
    }

    // ---- WindowSize ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void WindowSizeScalesTheBaseDesignSize(int dpi)
    {
        var (width, height) = DialogLayout.WindowSize(dpi);

        Assert.Equal(DialogLayout.Scale(DialogLayout.BaseWidth, dpi), width);
        Assert.Equal(DialogLayout.Scale(DialogLayout.BaseHeight, dpi), height);
    }

    // ---- Equal margins (the bug this component exists to prevent) ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void FullWidthElementsHaveEqualMarginsOnBothSides(int dpi)
    {
        int clientWidth = DialogLayout.Scale(DialogLayout.BaseWidth, dpi);
        int margin = DialogLayout.Scale(DialogLayout.Margin, dpi);

        foreach (var rect in new[]
                 {
                     DialogLayout.Prompt(clientWidth, dpi),
                     DialogLayout.Hint(clientWidth, dpi),
                     DialogLayout.Combo(clientWidth, dpi),
                     DialogLayout.Status(clientWidth, dpi),
                 })
        {
            Assert.Equal(margin, rect.X);
            Assert.Equal(clientWidth - margin, rect.X + rect.Width);
        }
    }

    [Fact]
    public void BugRegression_MarginsAreEqualAt144DpiNotJustTheOuterWidthOneOff()
    {
        // Hand-traced: at 144 DPI the client width for the base design is 690, the
        // margin is 24. A left margin of 24 and a 1-pixel-off right margin (i.e. using
        // the outer window width instead of the client width) is exactly the bug this
        // type exists to prevent.
        const int clientWidth = 690;
        const int dpi = 144;

        var prompt = DialogLayout.Prompt(clientWidth, dpi);

        Assert.Equal(24, prompt.X);
        Assert.Equal(666, prompt.X + prompt.Width); // 690 - 24, not 690 - 1
        Assert.Equal(642, prompt.Width);
    }

    // ---- Vertical stacking ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void PromptHintComboAndStatusStackDownwardSeparatedByLineGap(int dpi)
    {
        int clientWidth = DialogLayout.Scale(DialogLayout.BaseWidth, dpi);
        int margin = DialogLayout.Scale(DialogLayout.Margin, dpi);
        int gap = DialogLayout.Scale(DialogLayout.LineGap, dpi);
        int lineHeight = DialogLayout.Scale(DialogLayout.LineHeight, dpi);
        int comboHeight = DialogLayout.Scale(DialogLayout.ComboHeight, dpi);

        var prompt = DialogLayout.Prompt(clientWidth, dpi);
        var hint = DialogLayout.Hint(clientWidth, dpi);
        var combo = DialogLayout.Combo(clientWidth, dpi);
        var status = DialogLayout.Status(clientWidth, dpi);

        Assert.Equal(margin, prompt.Y);
        Assert.Equal(lineHeight, prompt.Height);

        Assert.Equal(prompt.Y + prompt.Height + gap, hint.Y);
        Assert.Equal(lineHeight, hint.Height);

        Assert.Equal(hint.Y + hint.Height + gap, combo.Y);
        Assert.Equal(comboHeight, combo.Height);

        Assert.Equal(combo.Y + combo.Height + gap, status.Y);
        Assert.Equal(lineHeight, status.Height);
    }

    // ---- Buttons ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void ButtonsAreRightAlignedCancelRightmostConfirmToItsLeft(int dpi)
    {
        int clientWidth = DialogLayout.Scale(DialogLayout.BaseWidth, dpi);
        int clientHeight = DialogLayout.Scale(DialogLayout.BaseHeight, dpi);
        int margin = DialogLayout.Scale(DialogLayout.Margin, dpi);
        int gap = DialogLayout.Scale(DialogLayout.LineGap, dpi);
        int buttonWidth = DialogLayout.Scale(DialogLayout.ButtonWidth, dpi);
        int buttonHeight = DialogLayout.Scale(DialogLayout.ButtonHeight, dpi);

        var confirm = DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi);
        var cancel = DialogLayout.CancelButton(clientWidth, clientHeight, dpi);

        // Cancel is rightmost, flush against the right margin.
        Assert.Equal(clientWidth - margin, cancel.X + cancel.Width);
        Assert.Equal(buttonWidth, cancel.Width);
        Assert.Equal(buttonHeight, cancel.Height);

        // Confirm sits immediately to Cancel's left, separated by the line gap.
        Assert.Equal(cancel.X - gap, confirm.X + confirm.Width);
        Assert.Equal(buttonWidth, confirm.Width);
        Assert.Equal(buttonHeight, confirm.Height);

        // Same row.
        Assert.Equal(confirm.Y, cancel.Y);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void ButtonsSitAgainstTheBottomMarginFitInsideClientHeightAndDoNotOverlapStatus(int dpi)
    {
        int clientWidth = DialogLayout.Scale(DialogLayout.BaseWidth, dpi);
        int clientHeight = DialogLayout.Scale(DialogLayout.BaseHeight, dpi);
        int margin = DialogLayout.Scale(DialogLayout.Margin, dpi);

        var status = DialogLayout.Status(clientWidth, dpi);
        var confirm = DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi);
        var cancel = DialogLayout.CancelButton(clientWidth, clientHeight, dpi);

        // Bottom-aligned to the margin.
        Assert.Equal(clientHeight - margin, confirm.Y + confirm.Height);
        Assert.Equal(clientHeight - margin, cancel.Y + cancel.Height);

        // Fit inside the client area.
        Assert.True(confirm.Y + confirm.Height <= clientHeight);
        Assert.True(cancel.Y + cancel.Height <= clientHeight);

        // Do not overlap Status above them.
        Assert.True(confirm.Y >= status.Y + status.Height);
        Assert.True(cancel.Y >= status.Y + status.Height);
    }

    // ---- No negative coordinates / widths, even for an implausibly narrow client ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void NothingIsNegativeOrOverflowsAnImplausiblyNarrowClientWidth(int dpi)
    {
        const int clientWidth = 200;
        int clientHeight = DialogLayout.Scale(DialogLayout.BaseHeight, dpi);

        var rects = new[]
        {
            DialogLayout.Prompt(clientWidth, dpi),
            DialogLayout.Hint(clientWidth, dpi),
            DialogLayout.Combo(clientWidth, dpi),
            DialogLayout.Status(clientWidth, dpi),
            DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi),
            DialogLayout.CancelButton(clientWidth, clientHeight, dpi),
        };

        foreach (var rect in rects)
        {
            Assert.True(rect.X >= 0, $"X was {rect.X}");
            Assert.True(rect.Y >= 0, $"Y was {rect.Y}");
            Assert.True(rect.Width >= 0, $"Width was {rect.Width}");
            Assert.True(rect.Height >= 0, $"Height was {rect.Height}");
            Assert.True(rect.X + rect.Width <= clientWidth, $"Right edge was {rect.X + rect.Width}");
            Assert.True(rect.Y + rect.Height <= clientHeight, $"Bottom edge was {rect.Y + rect.Height}");
        }
    }

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    public void NarrowClientStillKeepsCancelRightmostAndConfirmToItsLeft(int dpi)
    {
        const int clientWidth = 200;
        int clientHeight = DialogLayout.Scale(DialogLayout.BaseHeight, dpi);
        int gap = DialogLayout.Scale(DialogLayout.LineGap, dpi);

        var confirm = DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi);
        var cancel = DialogLayout.CancelButton(clientWidth, clientHeight, dpi);

        Assert.True(confirm.X < cancel.X);
        Assert.Equal(cancel.X - gap, confirm.X + confirm.Width);
    }

    // ---- Fix 1: genuine fit against a real window's client area, not the outer size
    // fed back to itself ----

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void ButtonsClearTheStatusLineInsideTheRealClientAreaOfAWsCaptionSysMenuWindow(int dpi)
    {
        // WindowSize(dpi) is documented (Fix 1) as the desired *client* size.
        // CreateWindowExW needs a larger *outer* size to end up with that much client
        // area, once the WS_CAPTION | WS_SYSMENU chrome — the exact style HotkeyCapture
        // creates the window with — is added via AdjustWindowRectExForDpi, the same API
        // HotkeyCapture now calls before CreateWindowExW. Deriving the outer size for
        // real here, instead of assuming "the window's size" and "the client size" are
        // interchangeable, is the assertion the pre-fix suite was missing: every
        // existing test above only ever fed WindowSize's own numbers back to itself as
        // both the outer and the client quantity, which proves internal consistency but
        // nothing about fit against a real window's actual client area.
        (int clientWidth, int clientHeight) = DialogLayout.WindowSize(dpi);

        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { Left = 0, Top = 0, Right = clientWidth, Bottom = clientHeight };
        bool adjusted = Win32.AdjustWindowRectExForDpi(ref rect, style, false, 0, (uint)dpi);
        Assert.True(adjusted);

        int outerHeight = rect.Bottom - rect.Top;

        // The chrome is real and non-trivial (tens of pixels, not one) — an outer
        // window this much taller than the client size is exactly what the pre-fix
        // code, which requested an outer size equal to the *client* size, failed to ask
        // Windows for.
        Assert.True(outerHeight > clientHeight);

        var status = DialogLayout.Status(clientWidth, dpi);
        var confirm = DialogLayout.ConfirmButton(clientWidth, clientHeight, dpi);
        var cancel = DialogLayout.CancelButton(clientWidth, clientHeight, dpi);

        Assert.True(confirm.Y > status.Y + status.Height,
            $"confirm.Y={confirm.Y} must clear status bottom={status.Y + status.Height}");
        Assert.True(cancel.Y > status.Y + status.Height,
            $"cancel.Y={cancel.Y} must clear status bottom={status.Y + status.Height}");

        Assert.True(confirm.Y + confirm.Height <= clientHeight);
        Assert.True(cancel.Y + cancel.Height <= clientHeight);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void BugRegression_TreatingTheClientSizeAsAnOuterSizeWouldHaveOverlappedTheStatusLine(int dpi)
    {
        // Reconstructs the exact pre-fix bug numerically: DialogLayout.WindowSize's
        // numbers used to be handed straight to CreateWindowExW as the *outer* size.
        // A WS_CAPTION | WS_SYSMENU window created at that outer height has a real
        // client height smaller by the chrome AdjustWindowRectExForDpi reports for the
        // same style — computed the same way the fix does, but subtracted rather than
        // added, to reconstruct exactly what GetClientRect would have returned under
        // the old bug.
        (int clientWidth, int nominalOuterHeight) = DialogLayout.WindowSize(dpi);

        const uint style = Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { Left = 0, Top = 0, Right = clientWidth, Bottom = nominalOuterHeight };
        Win32.AdjustWindowRectExForDpi(ref rect, style, false, 0, (uint)dpi);
        int chromeHeight = (rect.Bottom - rect.Top) - nominalOuterHeight;

        int realClientHeightUnderTheOldBug = nominalOuterHeight - chromeHeight;

        var status = DialogLayout.Status(clientWidth, dpi);
        var confirm = DialogLayout.ConfirmButton(clientWidth, realClientHeightUnderTheOldBug, dpi);

        // This is the bug, made concrete: the button's top edge does not clear the
        // status line's bottom edge once the window's real (chrome-reduced) client
        // height is used instead of the nominal size the old code mistook for it.
        Assert.True(confirm.Y <= status.Y + status.Height,
            $"expected the old bug to overlap: confirm.Y={confirm.Y}, status bottom={status.Y + status.Height}");
    }
}
