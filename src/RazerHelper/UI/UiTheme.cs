using System.Runtime.InteropServices;

namespace RazerHelper.UI;

internal static class UiTheme
{
    private static readonly float DpiScale = GetDpiForSystem() / 96F;

    /// <summary>Windows' display scaling for this session, for example 2.25 at 225%.</summary>
    public static float WindowsScale => DpiScale;

    /// <summary>
    /// The multiplier for every size and font in the window (1 is the base design).
    /// Set once, before any window exists, by <see cref="SetScale"/>.
    /// </summary>
    public static float Scale { get; private set; } = 1F;

    /// <summary>Sets the window size multiplier. Call before creating any window: fonts and sizes already made keep the old one.</summary>
    public static void SetScale(float scale) => Scale = scale;

    /// <summary>A pixel size of the base design, at the current scale.</summary>
    public static int S(int pixels) => (int)Math.Round(pixels * Scale);

    public static float S(float pixels) => pixels * Scale;

    public static Padding S(Padding padding) =>
        new(S(padding.Left), S(padding.Top), S(padding.Right), S(padding.Bottom));

    public static Size S(Size size) => new(S(size.Width), S(size.Height));

    public static readonly Color BackgroundColor = Color.FromArgb(0x14, 0x14, 0x14);
    public static readonly Color ButtonColor = Color.FromArgb(0x1F, 0x1F, 0x1F);

    /// <summary>The 1px outline of an unselected button or drop-down.</summary>
    public static readonly Color ButtonBorderColor = Color.FromArgb(0x2E, 0x2E, 0x2E);

    /// <summary>
    /// What sections and rows sit on. The design has no cards any more, so
    /// this is the window's own color; kept as its own name so a card look
    /// can come back in one place.
    /// </summary>
    public static readonly Color CardColor = BackgroundColor;

    /// <summary>The window outline Windows draws.</summary>
    public static readonly Color BorderColor = Color.FromArgb(0x2E, 0x2E, 0x2E);
    public static readonly Color RazerGreen = Color.FromArgb(0x46, 0xD7, 0x2E);

    /// <summary>The text on a green (selected) button.</summary>
    public static readonly Color OnGreenTextColor = Color.FromArgb(0x1E, 0x1E, 0x1E);

    /// <summary>The unfilled part of a slider or a bar.</summary>
    public static readonly Color TrackColor = Color.FromArgb(0x33, 0x33, 0x33);

    /// <summary>Quiet secondary text: the model name, hints and notes.</summary>
    public static readonly Color SubtleTextColor = Color.FromArgb(145, 145, 145);

    /// <summary>
    /// The design's typeface, Inter, when it is installed; otherwise Segoe
    /// UI, Windows' own, which is close in shape and always there.
    /// </summary>
    public static readonly string FontFamilyName = IsInstalled("Inter") ? "Inter" : "Segoe UI";

    /// <summary>A font of the design's sizes, which are in pixels: 14px is 10.5pt.</summary>
    public static Font DesignFont(float pixels, FontStyle style = FontStyle.Regular) =>
        GetDesignFont(FontFamilyName, pixels * 0.75F, style);

    private static bool IsInstalled(string family)
    {
        using var fonts = new System.Drawing.Text.InstalledFontCollection();
        return fonts.Families.Any(installed => string.Equals(installed.Name, family, StringComparison.OrdinalIgnoreCase));
    }

    // The app only ever uses a handful of distinct fonts, and controls never
    // dispose a font they are handed, so each look is created once and shared.
    private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> DesignFonts = [];

    /// <summary>
    /// The shared font for this look, created the first time it is asked for.
    /// Never dispose it: every control that uses it shares the same object.
    /// </summary>
    /// <remarks>
    /// The popup uses AutoScaleMode.None, so fonts are sized against the system
    /// DPI here to keep the design surface stable across display scales, then
    /// grown by the window size multiplier along with everything else.
    /// </remarks>
    public static Font GetDesignFont(
        string familyName,
        float pointSize,
        FontStyle style = FontStyle.Regular)
    {
        lock (DesignFonts)
        {
            var key = (familyName, pointSize, style);

            if (!DesignFonts.TryGetValue(key, out var font))
                DesignFonts[key] = font = new Font(familyName, pointSize * Scale / DpiScale, style, GraphicsUnit.Point);

            return font;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
