using System.Runtime.InteropServices;
using DesktopIconsToggle.Native;

namespace DesktopIconsToggle;

/// <summary>
/// Renders icon-font glyphs into small bitmaps for the tray menu, sized for the owner
/// window's DPI and cached by glyph. Deliberately painted with the system menu colours
/// (<c>COLOR_MENU</c> / <c>COLOR_MENUTEXT</c>), not the app's own <see cref="Theme"/>:
/// Windows draws the menu itself with those colours, so matching them is what makes the
/// icons look right in the currently active theme. That match is only as fresh as the
/// last render, though — the colours are baked into each bitmap once and then cached
/// indefinitely, so a theme change while the app is running needs an explicit
/// <see cref="Invalidate"/> to be reflected; <see cref="Get"/> alone will keep returning
/// the stale-coloured bitmap from before the change.
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

    private readonly int _size;
    private readonly IntPtr _font;
    private readonly Dictionary<char, IntPtr> _cache = new();
    private bool _disposed;

    public MenuIcons(int dpi)
    {
        _size = 16 * dpi / 96;
        _font = CreateIconFont(_size);
    }

    /// <summary>
    /// A cached bitmap for the glyph, sized and coloured for the menu, or
    /// <see cref="IntPtr.Zero"/> when the icon font is unavailable — the caller then shows
    /// a text-only menu.
    /// </summary>
    public IntPtr Get(char glyph)
    {
        if (_font == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (_cache.TryGetValue(glyph, out IntPtr cached))
        {
            return cached;
        }

        IntPtr bitmap = Render(glyph);
        _cache[glyph] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Deletes every cached bitmap and clears the cache, leaving this instance otherwise
    /// usable — the next <see cref="Get"/> re-renders at the system menu colours current
    /// at that moment. Call when the system theme changes (see
    /// <c>WM_SETTINGCHANGE</c>/<c>ImmersiveColorSet</c> in <c>Program</c>), since otherwise
    /// every icon keeps the colours baked in at first render for the lifetime of the app.
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
                string actual = length > 0 ? new string(buffer, 0, length) : string.Empty;
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

    private IntPtr Render(char glyph)
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
                IntPtr bitmap = Win32.CreateCompatibleBitmap(screenDc, _size, _size);
                if (bitmap == IntPtr.Zero)
                {
                    return IntPtr.Zero;
                }

                IntPtr previousBitmap = Win32.SelectObject(memDc, bitmap);
                IntPtr previousFont = Win32.SelectObject(memDc, _font);

                var rect = new Win32.RECT { Left = 0, Top = 0, Right = _size, Bottom = _size };
                Win32.FillRect(memDc, ref rect, Win32.GetSysColorBrush(Win32.COLOR_MENU));

                Win32.SetBkMode(memDc, Win32.TRANSPARENT);
                Win32.SetTextColor(memDc, Win32.GetSysColor(Win32.COLOR_MENUTEXT));
                Win32.DrawTextW(memDc, glyph.ToString(), -1, ref rect,
                                Win32.DT_CENTER | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

                Win32.SelectObject(memDc, previousFont);
                Win32.SelectObject(memDc, previousBitmap);

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
