using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Draws the two tray icon states the notification area shows: a square frame with four
/// blue tiles while the desktop icons are visible, and the same frame, empty, while they are
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
    // COLORREF-style values (0x00BBGGRR), but grayscale so channel order is moot. Matches
    // make-icon.ps1's near-white/near-black frame (RGB 235,235,235 / 32,32,32).
    private const uint LightTaskbarColor = 0x00202020u; // near-black, for a light taskbar
    private const uint DarkTaskbarColor  = 0x00EBEBEBu; // near-white, for a dark taskbar

    // The app icon's tile blue, RGB(120, 180, 255), written as a COLORREF (0x00BBGGRR):
    // blue=0xFF, green=0xB4, red=0x78. Run through IconPalette.EnsureContrast before
    // painting so it stays visible against whichever taskbar theme is active.
    private const uint TileColor = 0x00FFB478u;

    private readonly Theme _theme;
    private IntPtr _visible;
    private IntPtr _hidden;

    public TrayIconArt(Theme theme)
    {
        _theme = theme;
        Refresh();
    }

    /// <summary>Square frame with four blue tiles — desktop icons are visible.</summary>
    public IntPtr Visible => _visible;

    /// <summary>The same square frame, empty — desktop icons are hidden.</summary>
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

        uint frameColor = _theme.IsSystemDark ? DarkTaskbarColor : LightTaskbarColor;
        uint tileColor = IconPalette.EnsureContrast(TileColor, _theme.IsSystemDark);

        IntPtr newVisible = Render(size, frameColor, tileColor, withTiles: true);
        IntPtr newHidden = Render(size, frameColor, tileColor, withTiles: false);

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
    private static IntPtr Render(int size, uint frameColorRef, uint tileColorRef, bool withTiles)
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

                Paint(bits, size, frameColorRef, tileColorRef, withTiles);

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
    /// Writes the square outline and, when <paramref name="withTiles"/> is true, the four
    /// interior tiles directly into the DIB's premultiplied BGRA buffer. Every pixel is
    /// written explicitly, drawn or not — this does not rely on the buffer's initial
    /// content. Every drawn pixel is fully opaque, so premultiplied equals the plain
    /// channel value (alpha 255: <c>channel * 255 / 255 == channel</c>); every other pixel
    /// is written as all-zero, i.e. fully transparent.
    ///
    /// Geometry mirrors <c>make-icon.ps1</c>'s 64 px canvas, scaled from <c>n</c>, the icon
    /// size: the frame stroke is <c>w = max(1, round(n / 16))</c>; the square is inset by
    /// <c>w</c> on every side and stroked <c>w</c> thick, so its interior spans
    /// <c>[2w, n - 2w)</c>. The four tiles are <c>max(2, round(n * 10 / 64))</c> square,
    /// with top-left corners at <c>round(n * 18 / 64)</c> and <c>round(n * 36 / 64)</c> on
    /// both axes — the script's 10 px tiles placed at 18 and 36 on its 64 px canvas.
    /// </summary>
    private static unsafe void Paint(IntPtr bits, int size, uint frameColorRef, uint tileColorRef, bool withTiles)
    {
        byte frameR = (byte)(frameColorRef & 0xFF);
        byte frameG = (byte)((frameColorRef >> 8) & 0xFF);
        byte frameB = (byte)((frameColorRef >> 16) & 0xFF);

        byte tileR = (byte)(tileColorRef & 0xFF);
        byte tileG = (byte)((tileColorRef >> 8) & 0xFF);
        byte tileB = (byte)((tileColorRef >> 16) & 0xFF);

        int w = Math.Max(1, (int)Math.Round(size / 16.0));
        int outerLo = w;
        int outerHi = size - w;      // exclusive
        int innerLo = 2 * w;
        int innerHi = size - 2 * w;  // exclusive

        int tileSide = Math.Max(2, (int)Math.Round(size * 10.0 / 64.0));
        int nearOffset = (int)Math.Round(size * 18.0 / 64.0);
        int farOffset = (int)Math.Round(size * 36.0 / 64.0);

        byte* pixels = (byte*)bits;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onOutline = x >= outerLo && x < outerHi && y >= outerLo && y < outerHi
                               && !(x >= innerLo && x < innerHi && y >= innerLo && y < innerHi);

                bool onTile = withTiles &&
                              (InSquare(x, y, nearOffset, nearOffset, tileSide) ||
                               InSquare(x, y, farOffset, nearOffset, tileSide) ||
                               InSquare(x, y, nearOffset, farOffset, tileSide) ||
                               InSquare(x, y, farOffset, farOffset, tileSide));

                byte* pixel = pixels + (y * size + x) * 4;
                if (onOutline)
                {
                    pixel[0] = frameB;
                    pixel[1] = frameG;
                    pixel[2] = frameR;
                    pixel[3] = 255;
                }
                else if (onTile)
                {
                    pixel[0] = tileB;
                    pixel[1] = tileG;
                    pixel[2] = tileR;
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

    /// <summary>Top-left-corner square test: true when (x, y) falls within a
    /// <paramref name="side"/>-pixel square whose top-left corner is (<paramref name="left"/>, <paramref name="top"/>).</summary>
    private static bool InSquare(int x, int y, int left, int top, int side)
    {
        return x >= left && x < left + side && y >= top && y < top + side;
    }

    public void Dispose()
    {
        DestroyIfPresent(_visible);
        DestroyIfPresent(_hidden);
        _visible = IntPtr.Zero;
        _hidden = IntPtr.Zero;
    }
}
