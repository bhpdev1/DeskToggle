using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Draws the two tray icon states the notification area shows: a square with four dots
/// while the desktop icons are visible, and the same square, empty, while they are
/// hidden — so the tray icon itself answers "are my icons on" at a glance. Built the way
/// <see cref="MenuIcons"/> builds its bitmaps — a 32-bit top-down <c>CreateDIBSection</c>
/// written directly as premultiplied BGRA — then turned into an <c>HICON</c> via
/// <c>CreateIconIndirect</c>. Sized from <c>SM_CXSMICON</c> so the art tracks DPI instead
/// of being fixed to 16 px, and coloured from <see cref="Theme.IsSystemDark"/> — the
/// taskbar/notification-area theme, which is independent of the app theme
/// <see cref="MenuIcons"/> uses for the popup menu.
/// </summary>
internal sealed class TrayIconArt : IDisposable
{
    // COLORREF-style values (0x00BBGGRR), but grayscale so channel order is moot.
    private const uint LightTaskbarColor = 0x00202020u; // near-black, for a light taskbar
    private const uint DarkTaskbarColor  = 0x00E8E8E8u; // near-white, for a dark taskbar

    private readonly Theme _theme;
    private IntPtr _visible;
    private IntPtr _hidden;

    public TrayIconArt(Theme theme)
    {
        _theme = theme;
        Refresh();
    }

    /// <summary>Square with four dots — desktop icons are visible.</summary>
    public IntPtr Visible => _visible;

    /// <summary>The same square, empty — desktop icons are hidden.</summary>
    public IntPtr Hidden => _hidden;

    /// <summary>
    /// Redraws both icons for the current system theme and icon metrics. Builds the two
    /// replacement icons first, then destroys whatever handles were previously held, and
    /// only then publishes the new ones — so a reader can never observe a destroyed handle
    /// through <see cref="Visible"/>/<see cref="Hidden"/>, and nothing leaks even if this
    /// runs back-to-back.
    /// </summary>
    public void Refresh()
    {
        int size = Win32.GetSystemMetrics(Win32.SM_CXSMICON);
        if (size <= 0)
        {
            size = 16;
        }

        uint color = _theme.IsSystemDark ? DarkTaskbarColor : LightTaskbarColor;

        IntPtr newVisible = Render(size, color, withDots: true);
        IntPtr newHidden = Render(size, color, withDots: false);

        DestroyIfPresent(_visible);
        DestroyIfPresent(_hidden);

        _visible = newVisible;
        _hidden = newHidden;
    }

    private static void DestroyIfPresent(IntPtr icon)
    {
        if (icon != IntPtr.Zero)
        {
            Win32.DestroyIcon(icon);
        }
    }

    /// <summary>
    /// Builds one HICON: a 32-bit top-down DIB painted directly as premultiplied BGRA
    /// (see <see cref="Paint"/>), paired with an all-zero monochrome mask — required by
    /// <c>ICONINFO</c>, but its content is ignored for a 32-bit colour bitmap that already
    /// carries real alpha. <c>CreateIconIndirect</c> copies both bitmaps into the icon it
    /// returns, so both are deleted here immediately afterwards on every path, success or
    /// failure: keeping them would leak, and the leak would repeat on every
    /// <see cref="Refresh"/>.
    /// </summary>
    private static IntPtr Render(int size, uint colorRef, bool withDots)
    {
        IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            try
            {
                var header = new Win32.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                    biWidth = size,
                    biHeight = -size, // negative: top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = Win32.BI_RGB,
                };

                IntPtr colorBitmap = Win32.CreateDIBSection(screenDc, ref header, Win32.DIB_RGB_COLORS,
                                                            out IntPtr bits, IntPtr.Zero, 0);
                if (colorBitmap == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                if (bits == IntPtr.Zero)
                {
                    Win32.DeleteObject(colorBitmap);
                    return IntPtr.Zero;
                }

                // As in MenuIcons.Render: the DIB must be deselected before its bits are
                // touched directly or the bitmap is deleted, while selected the DC still
                // owns it.
                IntPtr previousBitmap = Win32.SelectObject(memDc, colorBitmap);
                Win32.SelectObject(memDc, previousBitmap);

                Paint(bits, size, colorRef, withDots);

                IntPtr maskBitmap = Win32.CreateBitmap(size, size, 1, 1, IntPtr.Zero);
                if (maskBitmap == IntPtr.Zero)
                {
                    Win32.DeleteObject(colorBitmap);
                    return IntPtr.Zero;
                }

                var iconInfo = new Win32.ICONINFO
                {
                    fIcon = true,
                    xHotspot = 0,
                    yHotspot = 0,
                    hbmMask = maskBitmap,
                    hbmColor = colorBitmap,
                };

                IntPtr icon = Win32.CreateIconIndirect(ref iconInfo);

                Win32.DeleteObject(colorBitmap);
                Win32.DeleteObject(maskBitmap);

                return icon;
            }
            finally
            {
                Win32.DeleteDC(memDc);
            }
        }
        finally
        {
            Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Writes the square outline and, when <paramref name="withDots"/> is true, the four
    /// interior dots directly into the DIB's premultiplied BGRA buffer. Every pixel is
    /// written explicitly, drawn or not — this does not rely on the buffer's initial
    /// content. Every drawn pixel is fully opaque, so premultiplied equals the plain
    /// channel value (alpha 255: <c>channel * 255 / 255 == channel</c>); every other pixel
    /// is written as all-zero, i.e. fully transparent.
    ///
    /// Geometry, with <c>n</c> the icon size and <c>w = max(1, n / 8)</c> the stroke width:
    /// the square is inset by <c>w</c> on every side and stroked <c>w</c> thick, so its
    /// interior spans <c>[2w, n - 2w)</c>. The four dots are <c>max(2, n / 5)</c> square,
    /// centred on that interior's quarter points (at 1/4 and 3/4 along each axis), which
    /// places them as a 2x2 arrangement with clear space around them.
    /// </summary>
    private static unsafe void Paint(IntPtr bits, int size, uint colorRef, bool withDots)
    {
        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);

        int w = Math.Max(1, size / 8);
        int outerLo = w;
        int outerHi = size - w;      // exclusive
        int innerLo = 2 * w;
        int innerHi = size - 2 * w;  // exclusive

        int dotSide = Math.Max(2, size / 5);
        int interiorSize = innerHi - innerLo;
        int q1 = innerLo + interiorSize / 4;
        int q3 = innerLo + interiorSize * 3 / 4;

        byte* pixels = (byte*)bits;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onOutline = x >= outerLo && x < outerHi && y >= outerLo && y < outerHi
                               && !(x >= innerLo && x < innerHi && y >= innerLo && y < innerHi);

                bool onDot = withDots &&
                             (InSquare(x, y, q1, q1, dotSide) ||
                              InSquare(x, y, q3, q1, dotSide) ||
                              InSquare(x, y, q1, q3, dotSide) ||
                              InSquare(x, y, q3, q3, dotSide));

                byte* pixel = pixels + (y * size + x) * 4;
                if (onOutline || onDot)
                {
                    pixel[0] = b;
                    pixel[1] = g;
                    pixel[2] = r;
                    pixel[3] = 255;
                }
                else
                {
                    pixel[0] = 0;
                    pixel[1] = 0;
                    pixel[2] = 0;
                    pixel[3] = 0;
                }
            }
        }
    }

    private static bool InSquare(int x, int y, int centerX, int centerY, int side)
    {
        int half = side / 2;
        int loX = centerX - half;
        int loY = centerY - half;
        return x >= loX && x < loX + side && y >= loY && y < loY + side;
    }

    public void Dispose()
    {
        DestroyIfPresent(_visible);
        DestroyIfPresent(_hidden);
        _visible = IntPtr.Zero;
        _hidden = IntPtr.Zero;
    }
}
