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

    public static readonly Color BackgroundColor = Color.FromArgb(0x15, 0x15, 0x15);

    /// <summary>An unselected button's or drop-down's flat fill.</summary>
    public static readonly Color ButtonColor = Color.FromArgb(0x24, 0x24, 0x24);

    /// <summary>A button's fill under the pointer: a touch lighter.</summary>
    public static readonly Color ButtonHoverColor = Color.FromArgb(0x30, 0x30, 0x30);

    /// <summary>The thin lines between sections and between rows.</summary>
    public static readonly Color DividerColor = Color.FromArgb(0x20, 0x20, 0x20);

    /// <summary>The 1px outline of menus and other quiet edges.</summary>
    public static readonly Color ButtonBorderColor = Color.FromArgb(0x2E, 0x2E, 0x2E);

    /// <summary>
    /// What sections and rows sit on. The design has no cards any more, so
    /// this is the window's own color; kept as its own name so a card look
    /// can come back in one place.
    /// </summary>
    public static readonly Color CardColor = BackgroundColor;

    /// <summary>The line between the tray menu's items.</summary>
    public static readonly Color BorderColor = Color.FromArgb(0x2E, 0x2E, 0x2E);
    public static readonly Color RazerGreen = Color.FromArgb(0x46, 0xD7, 0x2E);

    /// <summary>A selected button's fill under the pointer: the green a touch lighter.</summary>
    public static readonly Color RazerGreenHover = Color.FromArgb(0x5A, 0xE0, 0x44);

    /// <summary>The circle behind a performance mode's icon: darker green on the selected button, darker grey on the rest.</summary>
    public static readonly Color SelectedIconCircleColor = Color.FromArgb(0x37, 0xAA, 0x24);
    public static readonly Color IconCircleColor = Color.FromArgb(0x1F, 0x1F, 0x1F);

    /// <summary>The text and icons on a green (selected) button: nearly the window's own dark.</summary>
    public static readonly Color OnGreenTextColor = Color.FromArgb(0x18, 0x18, 0x18);

    /// <summary>The unfilled part of a slider or a bar.</summary>
    public static readonly Color TrackColor = Color.FromArgb(0x2E, 0x2E, 0x2E);

    /// <summary>An off switch's track, and an unavailable radio option's dot.</summary>
    public static readonly Color OffColor = Color.FromArgb(0x5F, 0x5F, 0x5F);

    /// <summary>Quiet secondary text: descriptions, hints and values.</summary>
    public static readonly Color SubtleTextColor = Color.FromArgb(0x9D, 0x9D, 0x9D);

    /// <summary>
    /// The design's typeface, Titillium Web, which comes inside the exe;
    /// should it fail to load, Segoe UI, Windows' own, which is always there.
    /// </summary>
    public static readonly string FontFamilyName = AppFonts.IsLoaded ? AppFonts.Family : "Segoe UI";

    /// <summary>A font of the design's sizes, which are in pixels: 14px is 10.5pt.</summary>
    public static Font DesignFont(float pixels, FontStyle style = FontStyle.Regular) =>
        GetDesignFont(FontFamilyName, pixels * 0.75F, style);

    // One point, in the design's pixels.
    private const float OnePoint = 4F / 3;

    /// <summary>A title's or a button's font: one point smaller again than other text of its size.</summary>
    public static Font TitleFont(float pixels, FontStyle style = FontStyle.Regular) => DesignFont(pixels - OnePoint, style);

    /// <summary>The semi-bold <see cref="TitleFont"/>.</summary>
    public static Font SemiBoldTitleFont(float pixels) => SemiBoldFont(pixels - OnePoint);

    /// <summary>The capitalised titles (sections, windows) and the figures beside them: 16px bold, a point over the other titles.</summary>
    public static Font CapsTitleFont() => DesignFont(16, FontStyle.Bold);

    /// <summary>A semi-bold font of the design's sizes, for labels a step below titles; bold where the semi-bold weight is missing.</summary>
    public static Font SemiBoldFont(float pixels) =>
        AppFonts.Find(AppFonts.SemiBoldFamily) is not null
            ? GetDesignFont(AppFonts.SemiBoldFamily, pixels * 0.75F)
            : DesignFont(pixels, FontStyle.Bold);

    // The app only ever uses a handful of distinct fonts, and controls never
    // dispose a font they are handed, so each look is created once and shared.
    private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> DesignFonts = [];

    // Taken off every font size; see GetDesignFont.
    private const float TextSizeOffsetPoints = 1F;

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
            {
                // Every text one point smaller than the design's sizes (titles and buttons one more; see TitleFont).
                var size = (pointSize - TextSizeOffsetPoints) * Scale / DpiScale;

                // One of the exe's own families is only reachable as an object; a name finds installed fonts.
                DesignFonts[key] = font = AppFonts.Find(familyName) is { } family
                    ? new Font(family, size, style, GraphicsUnit.Point)
                    : new Font(familyName, size, style, GraphicsUnit.Point);
            }

            return font;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
