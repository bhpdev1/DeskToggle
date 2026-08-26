using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Renders the embedded Lucide alpha masks (see <see cref="LucideIcons"/>) into small
/// 32-bit premultiplied-alpha bitmaps for the tray menu, sized for the owner window's DPI
/// and cached by (icon, colour, alpha). Each bitmap carries a real alpha channel, so its
/// antialiased edges composite against whatever the menu actually paints behind them
/// rather than a colour guessed and baked in ahead of time — a theme flip doesn't stale an
/// already-rendered bitmap, it just renders (and caches) a new entry the next time a
/// different, contrast-corrected colour is requested for it. See <see cref="Invalidate"/>.
/// </summary>
internal sealed class MenuIcons : IDisposable
{
    // Per-icon colours the user picked and approved, as COLORREF (0x00BBGGRR — blue,
    // green, red). Writing the RGB literal directly here would swap red and blue.
    public const uint HideIconsColor = 0x00FFCD60u; // RGB(96, 205, 255)
    public const uint TaskbarColor   = 0x00FFA0BEu; // RGB(190, 160, 255)
    public const uint KeyboardColor  = 0x00CDE16Eu; // RGB(110, 225, 205)
    public const uint AutostartColor = 0x005ACDFFu; // RGB(255, 205, 90)
    public const uint QuitColor      = 0x007878FFu; // RGB(255, 120, 120)

    private readonly int _size;
    private readonly Theme _theme;
    private readonly Dictionary<(LucideIcons.Icon Icon, uint Color, byte Alpha), IntPtr> _cache = new();
    private bool _disposed;

    public MenuIcons(int dpi, Theme theme)
    {
        // Picking the nearest shipped raster size up front means the DIB and the mask are
        // always the same size — no scaling path is ever exercised.
        _size = LucideIcons.NearestSize(16 * dpi / 96);
        _theme = theme;
    }

    /// <summary>
    /// A cached bitmap for the icon rendered in the given COLORREF colour at the given
    /// alpha (0-255), sized for the menu.
    /// </summary>
    public IntPtr Get(LucideIcons.Icon icon, uint colorRef, byte alpha)
    {
        var key = (icon, colorRef, alpha);
        if (_cache.TryGetValue(key, out IntPtr cached))
        {
            return cached;
        }

        IntPtr bitmap = Render(icon, colorRef, alpha);
        _cache[key] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Lifts or darkens <paramref name="colorRef"/> just enough to contrast against the
    /// current theme's menu background, via <see cref="IconPalette.EnsureContrast"/>.
    /// </summary>
    public uint Contrast(uint colorRef) => IconPalette.EnsureContrast(colorRef, _theme.IsDark);

    /// <summary>
    /// Deletes every cached bitmap and clears the cache, and re-reads the light/dark
    /// preference so a theme flip is picked up by the next <see cref="Contrast"/> call.
    /// Call when the system settings change (see <c>WM_SETTINGCHANGE</c>/
    /// <c>ImmersiveColorSet</c> in <c>Program</c>): <see cref="IconPalette.EnsureContrast"/>
    /// depends on the theme, so a light/dark switch must re-render every icon.
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
        _theme.Refresh();
    }

    /// <summary>
    /// Creates a 32-bit top-down DIB section sized <see cref="_size"/> and fills it
    /// directly from the icon's alpha mask, coloured for <paramref name="colorRef"/> at
    /// <paramref name="alpha"/>. No GDI drawing is involved: the mask already carries the
    /// antialiased coverage, so there is no font to be missing and no glyph to fall back
    /// to an empty box.
    /// </summary>
    private IntPtr Render(LucideIcons.Icon icon, uint colorRef, byte alpha)
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
                // The DIB must be deselected before its bits are touched directly or the
                // bitmap is deleted: while selected, the DC still owns it.
                Win32.SelectObject(memDc, previousBitmap);

                byte[] mask = LucideIcons.Mask(icon, _size);
                Paint(bits, mask, _size, colorRef, alpha);

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
    /// Walks a <paramref name="size"/> x <paramref name="size"/> 32-bit BGRA buffer and
    /// writes each pixel as premultiplied BGRA for <paramref name="colorRef"/> (COLORREF,
    /// 0x00BBGGRR) at <paramref name="alpha"/>: the mask's own coverage byte is first
    /// scaled by the requested <paramref name="alpha"/> (so a dimmed, inactive icon is just
    /// this multiplied down), giving the pixel's true alpha <c>a</c>; each channel then
    /// becomes <c>channel * a / 255</c>. A fully covered, fully opaque pixel (a=255)
    /// becomes the opaque chosen colour; a fully uncovered one (a=0) becomes all-zero,
    /// i.e. fully transparent.
    /// </summary>
    private static unsafe void Paint(IntPtr bits, byte[] mask, int size, uint colorRef, byte alpha)
    {
        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);

        byte* pixels = (byte*)bits;
        int count = size * size;
        for (int i = 0; i < count; i++)
        {
            byte* pixel = pixels + i * 4;
            byte a = (byte)(mask[i] * alpha / 255);
            pixel[0] = (byte)(b * a / 255);
            pixel[1] = (byte)(g * a / 255);
            pixel[2] = (byte)(r * a / 255);
            pixel[3] = a;
        }
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

        _disposed = true;
    }
}
