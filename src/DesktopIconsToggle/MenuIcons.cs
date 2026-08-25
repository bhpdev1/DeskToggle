using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Renders icon-font glyphs into small 32-bit premultiplied-alpha bitmaps for the tray
/// menu, sized for the owner window's DPI and cached by (glyph, colour). Each bitmap
/// carries a real alpha channel, so its antialiased edges composite against whatever the
/// menu actually paints behind them rather than a colour guessed and baked in ahead of
/// time — a theme flip no longer stales an already-rendered glyph, it just renders (and
/// caches) a new entry the next time a different colour is requested for it. The one
/// value that *is* memoized across renders is the Windows accent colour returned by
/// <see cref="AccentColor"/> (via <c>DwmGetColorizationColor</c>), so that one still goes
/// stale if the user changes it while the app is running — see <see cref="Invalidate"/>.
/// </summary>
/// <remarks>
/// Needs "Segoe Fluent Icons" (Windows 11) or, failing that, "Segoe MDL2 Assets"
/// (Windows 10). When neither resolves, every bitmap is unavailable and <see cref="Get"/>
/// always returns <see cref="IntPtr.Zero"/> so the caller can fall back to a text-only
/// menu instead of an empty gutter.
/// </remarks>
internal sealed class MenuIcons : IDisposable
{
    private static readonly string[] CandidateFamilies = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    /// <summary>Windows' own default accent colour, used when <c>DwmGetColorizationColor</c> fails.</summary>
    public const uint DefaultAccentColor = 0x0078D4;

    /// <summary>The colour for <c>Quitter</c>.</summary>
    public const uint RedColor = 0xC42B1C;

    /// <summary>
    /// <c>COLOR_MENUTEXT</c>, converted from COLORREF to the packed 0x00RRGGBB form used
    /// as the cache key and by <see cref="Premultiply"/>. Black in light mode, white in
    /// dark mode, so it reads as neutral in both without any theme detection of our own.
    /// </summary>
    public static uint NeutralColor => ToRgb(Win32.GetSysColor(Win32.COLOR_MENUTEXT));

    private readonly int _size;
    private readonly IntPtr _font;
    private readonly Dictionary<(char Glyph, uint Color), IntPtr> _cache = new();
    private uint? _accentColor;
    private bool _disposed;

    public MenuIcons(int dpi)
    {
        _size = 16 * dpi / 96;
        _font = CreateIconFont(_size);
    }

    /// <summary>
    /// A cached bitmap for the glyph rendered in the given 0x00RRGGBB colour, sized for
    /// the menu, or <see cref="IntPtr.Zero"/> when the icon font is unavailable — the
    /// caller then shows a text-only menu.
    /// </summary>
    public IntPtr Get(char glyph, uint color)
    {
        if (_font == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var key = (glyph, color);
        if (_cache.TryGetValue(key, out IntPtr cached))
        {
            return cached;
        }

        IntPtr bitmap = Render(glyph, color);
        _cache[key] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// The user's Windows accent colour (low 24 bits of <c>DwmGetColorizationColor</c>,
    /// already in 0x00RRGGBB form), read once and memoized until the next
    /// <see cref="Invalidate"/>. Falls back to <see cref="DefaultAccentColor"/> if the
    /// call fails.
    /// </summary>
    public uint AccentColor()
    {
        if (_accentColor is uint cached)
        {
            return cached;
        }

        uint value = DefaultAccentColor;
        if (Win32.DwmGetColorizationColor(out uint colorization, out _) == 0)
        {
            value = colorization & 0x00FFFFFF;
        }

        _accentColor = value;
        return value;
    }

    /// <summary>
    /// Deletes every cached bitmap and clears the cache, and forgets the memoized
    /// Windows accent colour so the next call to <see cref="AccentColor"/> re-reads it
    /// via <c>DwmGetColorizationColor</c> instead of keeping the value read at first use.
    /// Call when the system settings change (see
    /// <c>WM_SETTINGCHANGE</c>/<c>ImmersiveColorSet</c> in <c>Program</c>) — the glyph
    /// bitmaps' own colours no longer need this to stay correct (they carry real alpha
    /// and are keyed by colour), but the memoized accent colour does.
    /// </summary>
    public void Invalidate()
    {
        foreach (IntPtr bitmap in _cache.Values)
        {
            if (bitmap != IntPtr.Zero)
            {
                Win32.DeleteObject(bitmap);
            }
        }

        _cache.Clear();
        _accentColor = null;
    }

    private static IntPtr CreateIconFont(int size)
    {
        foreach (string family in CandidateFamilies)
        {
            var logFont = new Win32.LOGFONTW
            {
                lfHeight = -size,
                lfCharSet = 1, // DEFAULT_CHARSET
                lfFaceName = family,
            };

            IntPtr font = Win32.CreateFontIndirectW(ref logFont);
            if (font == IntPtr.Zero)
            {
                continue;
            }

            if (ResolvesTo(font, family))
            {
                return font;
            }

            // GDI silently substitutes a fallback face when the requested one is not
            // installed; confirm the real name before trusting this handle.
            Win32.DeleteObject(font);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Selects the font into a screen DC and reads back the face GDI actually picked,
    /// the only way to tell a real match from a silent fallback substitution.
    /// </summary>
    private static bool ResolvesTo(IntPtr font, string family)
    {
        IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr previous = Win32.SelectObject(screenDc, font);
            try
            {
                var buffer = new char[64];
                int length = Win32.GetTextFaceW(screenDc, buffer.Length, buffer);
                if (length <= 0)
                {
                    return false;
                }

                // GetTextFaceW counts the terminating null in its return value, so the
                // naive substring carries a trailing '\0' and never compares equal.
                int end = Array.IndexOf(buffer, '\0', 0, length);
                string actual = new string(buffer, 0, end >= 0 ? end : length);
                return string.Equals(actual, family, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Win32.SelectObject(screenDc, previous);
            }
        }
        finally
        {
            Win32.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Renders <paramref name="glyph"/> into a 32-bit top-down DIB section: white glyph
    /// on a black background, GDI text mode (no native alpha), then walks the raw pixels
    /// via <see cref="Premultiply"/> to turn that greyscale coverage into a premultiplied
    /// BGRA bitmap coloured for <paramref name="color"/>.
    /// </summary>
    private IntPtr Render(char glyph, uint color)
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
                    biWidth = _size,
                    biHeight = -_size, // negative: top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = Win32.BI_RGB,
                };

                IntPtr bitmap = Win32.CreateDIBSection(screenDc, ref header, Win32.DIB_RGB_COLORS,
                                                       out IntPtr bits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                if (bits == IntPtr.Zero)
                {
                    Win32.DeleteObject(bitmap);
                    return IntPtr.Zero;
                }

                IntPtr previousBitmap = Win32.SelectObject(memDc, bitmap);
                IntPtr previousFont = Win32.SelectObject(memDc, _font);

                var rect = new Win32.RECT { Left = 0, Top = 0, Right = _size, Bottom = _size };
                Win32.FillRect(memDc, ref rect, Win32.GetStockObject(Win32.BLACK_BRUSH));

                Win32.SetBkMode(memDc, Win32.TRANSPARENT);
                Win32.SetTextColor(memDc, 0x00FFFFFF); // pure white: coverage becomes alpha below
                Win32.DrawTextW(memDc, glyph.ToString(), -1, ref rect,
                                Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

                Win32.SelectObject(memDc, previousFont);
                // The DIB must be deselected before its bits are touched directly or the
                // bitmap is deleted: while selected, the DC still owns it.
                Win32.SelectObject(memDc, previousBitmap);

                Premultiply(bits, _size, color);

                return bitmap;
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
    /// Walks a <paramref name="size"/> x <paramref name="size"/> 32-bit BGRA buffer whose
    /// blue byte currently holds white-on-black text coverage (grey, since B=G=R after
    /// GDI's antialiasing) and rewrites each pixel as premultiplied BGRA for
    /// <paramref name="color"/>: alpha <c>a</c> is the coverage, and each channel becomes
    /// <c>channel * a / 255</c>. A fully covered pixel (a=255) becomes the opaque chosen
    /// colour; a fully uncovered one (a=0) becomes all-zero, i.e. fully transparent.
    /// </summary>
    private static unsafe void Premultiply(IntPtr bits, int size, uint color)
    {
        byte r = (byte)((color >> 16) & 0xFF);
        byte g = (byte)((color >> 8) & 0xFF);
        byte b = (byte)(color & 0xFF);

        byte* pixels = (byte*)bits;
        int count = size * size;
        for (int i = 0; i < count; i++)
        {
            byte* pixel = pixels + i * 4;
            byte a = pixel[0]; // blue byte carries the glyph's antialiased coverage
            pixel[0] = (byte)(b * a / 255);
            pixel[1] = (byte)(g * a / 255);
            pixel[2] = (byte)(r * a / 255);
            pixel[3] = a;
        }
    }

    /// <summary>Converts a COLORREF (0x00bbggrr, red in the low byte) to packed 0x00RRGGBB.</summary>
    private static uint ToRgb(uint colorRef)
    {
        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);
        return ((uint)r << 16) | ((uint)g << 8) | b;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (IntPtr bitmap in _cache.Values)
        {
            if (bitmap != IntPtr.Zero)
            {
                Win32.DeleteObject(bitmap);
            }
        }

        _cache.Clear();

        if (_font != IntPtr.Zero)
        {
            Win32.DeleteObject(_font);
        }

        _disposed = true;
    }
}
