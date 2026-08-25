namespace DesktopIconsToggle;

/// <summary>A rectangle in client coordinates.</summary>
internal readonly record struct LayoutRect(int X, int Y, int Width, int Height);

/// <summary>
/// Computes the capture dialog's layout from its client size and DPI. Pure, so the
/// arithmetic the dialog got wrong — margins measured against the outer window width
/// rather than the client width — is covered by tests rather than by inspection.
/// </summary>
internal static class DialogLayout
{
    /// <summary>Design sizes, in 96-DPI units.</summary>
    public const int BaseWidth = 460;
    public const int BaseHeight = 210;
    public const int Margin = 16;
    public const int LineGap = 10;
    public const int LineHeight = 20;
    public const int ComboHeight = 34;
    public const int ButtonWidth = 110;
    public const int ButtonHeight = 30;

    /// <summary>Scales a 96-DPI design value to the given DPI.</summary>
    public static int Scale(int value, int dpi)
        => (int)Math.Round(value * (double)dpi / 96.0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The dialog's desired client size at a given DPI — the size every other method in
    /// this class lays out against. This is not the size to pass to CreateWindowExW: the
    /// caller must convert it to an outer window size (e.g. via AdjustWindowRectExForDpi,
    /// using the same style/DPI) before creating the window, since the outer size also
    /// includes the caption and borders.
    /// </summary>
    public static (int Width, int Height) WindowSize(int dpi)
        => (Scale(BaseWidth, dpi), Scale(BaseHeight, dpi));

    public static LayoutRect Prompt(int clientWidth, int dpi)
    {
        int margin = Scale(Margin, dpi);
        int width = FullWidth(clientWidth, margin);
        return new LayoutRect(margin, margin, width, Scale(LineHeight, dpi));
    }

    public static LayoutRect Hint(int clientWidth, int dpi)
    {
        int margin = Scale(Margin, dpi);
        int gap = Scale(LineGap, dpi);
        int lineHeight = Scale(LineHeight, dpi);
        int width = FullWidth(clientWidth, margin);
        int y = margin + lineHeight + gap;
        return new LayoutRect(margin, y, width, lineHeight);
    }

    public static LayoutRect Combo(int clientWidth, int dpi)
    {
        int margin = Scale(Margin, dpi);
        int gap = Scale(LineGap, dpi);
        int lineHeight = Scale(LineHeight, dpi);
        int width = FullWidth(clientWidth, margin);
        int y = margin + 2 * (lineHeight + gap);
        return new LayoutRect(margin, y, width, Scale(ComboHeight, dpi));
    }

    public static LayoutRect Status(int clientWidth, int dpi)
    {
        int margin = Scale(Margin, dpi);
        int gap = Scale(LineGap, dpi);
        int lineHeight = Scale(LineHeight, dpi);
        int comboHeight = Scale(ComboHeight, dpi);
        int width = FullWidth(clientWidth, margin);
        int y = margin + 2 * (lineHeight + gap) + comboHeight + gap;
        return new LayoutRect(margin, y, width, lineHeight);
    }

    public static LayoutRect ConfirmButton(int clientWidth, int clientHeight, int dpi)
        => Buttons(clientWidth, clientHeight, dpi).Confirm;

    public static LayoutRect CancelButton(int clientWidth, int clientHeight, int dpi)
        => Buttons(clientWidth, clientHeight, dpi).Cancel;

    /// <summary>Full-width row: spans from the left margin to the right margin.</summary>
    private static int FullWidth(int clientWidth, int margin)
        => Math.Max(0, clientWidth - 2 * margin);

    /// <summary>
    /// Lays out both buttons together so Cancel's right edge and Confirm's position
    /// relative to it are computed once and stay consistent between the two public
    /// accessors. Button width shrinks (never below zero) when the client is too
    /// narrow to fit both buttons plus the gap between them at their design width,
    /// so neither button ever gets a negative coordinate or spills past the margins.
    /// </summary>
    private static (LayoutRect Confirm, LayoutRect Cancel) Buttons(int clientWidth, int clientHeight, int dpi)
    {
        int margin = Scale(Margin, dpi);
        int gap = Scale(LineGap, dpi);
        int buttonHeight = Scale(ButtonHeight, dpi);
        int buttonWidth = Scale(ButtonWidth, dpi);

        int available = FullWidth(clientWidth, margin);
        int desiredTotal = 2 * buttonWidth + gap;
        if (desiredTotal > available)
        {
            buttonWidth = Math.Max(0, (available - gap) / 2);
        }

        int y = Math.Max(0, clientHeight - margin - buttonHeight);
        int cancelX = Math.Max(0, clientWidth - margin - buttonWidth);
        int confirmX = Math.Max(0, cancelX - gap - buttonWidth);

        var confirm = new LayoutRect(confirmX, y, buttonWidth, buttonHeight);
        var cancel = new LayoutRect(cancelX, y, buttonWidth, buttonHeight);
        return (confirm, cancel);
    }
}
