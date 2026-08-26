namespace DesktopIconsToggle;

/// <summary>Formats a Win32 RegisterHotKey modifier/virtual-key pair as a human-readable French string.</summary>
internal static class HotkeyFormatter
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    /// <summary>
    /// Virtual keys that, held with Ctrl alone, are near-universal application
    /// conventions (browser tab switching, editor save/find, clipboard, and so on) —
    /// the exact combinations a global hotkey would take over everywhere.
    /// </summary>
    private static readonly HashSet<uint> CommonAppKeysWithCtrl =
    [
        0x09, // Tab
        0x57, // W
        0x53, // S
        0x46, // F
        0x43, // C
        0x56, // V
        0x58, // X
        0x5A, // Z
        0x41, // A
        0x4E, // N
        0x54, // T
        0x50, // P
    ];

    /// <summary>
    /// True for combinations applications commonly bind, which a global hotkey would take
    /// over everywhere. Not refused — the user may want one — but worth telling them.
    /// </summary>
    public static bool IsCommonlyUsedByApps(uint modifiers, uint virtualKey)
    {
        if ((modifiers & ModWin) != 0)
        {
            // Nothing in this family is an application convention once Win is held.
            return false;
        }

        if ((modifiers & ModControl) == 0)
        {
            return false;
        }

        return CommonAppKeysWithCtrl.Contains(virtualKey);
    }

    public static string Format(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>(5);

        if ((modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }

        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Maj");
        }

        parts.Add(KeyName(virtualKey));

        return string.Join("+", parts);
    }

    private static string KeyName(uint virtualKey)
    {
        if (virtualKey >= 0x41 && virtualKey <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey >= 0x30 && virtualKey <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey >= 0x70 && virtualKey <= 0x87)
        {
            return "F" + (virtualKey - 0x70 + 1);
        }

        switch (virtualKey)
        {
            case 0x1B: return "Échap";
            case 0x20: return "Espace";
            case 0x09: return "Tab";
            case 0x0D: return "Entrée";
            case 0x08: return "Retour";
            case 0x2E: return "Suppr";
            case 0x2D: return "Inser";
            case 0x24: return "Origine";
            case 0x23: return "Fin";
            case 0x21: return "Page préc.";
            case 0x22: return "Page suiv.";
            case 0x25: return "Gauche";
            case 0x26: return "Haut";
            case 0x27: return "Droite";
            case 0x28: return "Bas";
            default: return $"Touche 0x{virtualKey:X2}";
        }
    }
}
