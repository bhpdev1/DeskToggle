using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// The font Windows uses for its own dialogs (Segoe UI 9pt on Windows 10 and 11),
/// read via <c>SPI_GETNONCLIENTMETRICS</c> rather than hard-coded, so the dialog
/// matches whatever the shell is actually configured to use.
/// </summary>
internal sealed class SystemFont : IDisposable
{
    public IntPtr Handle { get; private set; }

    /// <summary>
    /// Creates the system message font scaled to the given DPI, optionally at a larger
    /// point size for emphasis. Falls back to the stock DEFAULT_GUI_FONT when the metrics
    /// cannot be read, so the caller always gets a usable handle.
    /// </summary>
    public SystemFont(int dpi, int extraPoints = 0)
    {
        var metrics = new Win32.NONCLIENTMETRICSW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NONCLIENTMETRICSW>(),
        };

        if (Win32.SystemParametersInfoW(Win32.SPI_GETNONCLIENTMETRICS, metrics.cbSize, ref metrics, 0))
        {
            Win32.LOGFONTW font = metrics.lfMessageFont;
            font.lfHeight = ScaledHeight(font.lfHeight, dpi, extraPoints);

            Handle = Win32.CreateFontIndirectW(ref font);
        }

        if (Handle == IntPtr.Zero)
        {
            Handle = Win32.GetStockObject(Win32.DEFAULT_GUI_FONT);
        }
    }

    /// <summary>
    /// The metrics returned by <c>SPI_GETNONCLIENTMETRICS</c> are expressed at 96 DPI.
    /// <paramref name="lfHeight"/> is negative (character height convention); the
    /// arithmetic runs on the absolute value and the sign is restored at the end.
    /// <paramref name="extraPoints"/> is converted to pixels-at-96-DPI (1 point = 96/72
    /// pixels) before the whole magnitude is scaled to the target DPI, so emphasis and
    /// DPI scaling compose in a single rounding step.
    /// </summary>
    public static int ScaledHeight(int lfHeight, int dpi, int extraPoints)
    {
        int magnitude96 = Math.Abs(lfHeight) + (int)Math.Round(extraPoints * 96.0 / 72.0, MidpointRounding.AwayFromZero);
        int scaled = (int)Math.Round(magnitude96 * (dpi / 96.0), MidpointRounding.AwayFromZero);
        return -scaled;
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            Win32.DeleteObject(Handle);
            Handle = IntPtr.Zero;
        }
    }
}
