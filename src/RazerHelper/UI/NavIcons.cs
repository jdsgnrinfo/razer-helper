using System.Drawing.Drawing2D;
using System.Globalization;

namespace RazerHelper.UI;

/// <summary>The outlined pictures: the sidebar's, one per section, and the menu's three lines; and those before the settings rows.</summary>
internal enum NavIcon
{
    Performance,
    Lighting,
    Display,
    Audio,
    PowerProfiles,
    Optimize,
    System,
    Settings,
    Menu,

    // The settings rows' own pictures.
    Cpu,
    Gpu,
    ChargeLimit,
    Fan,
    Speaker,
    Plug,
    Battery,
    Memory,
    Trash,
    ShaderCache,
    FreeGpu,
    Moon,

    // The section titles' pictures.
    Monitor,
    Palette,
    RefreshRate,
    Keyboard,
    Logo,
    Equalizer
}

/// <summary>
/// Draws the sidebar's icons as thin outlines on a 24 by 24 grid, scaled to
/// whatever box they are given, with round ends, as the rest of the outlined icons.
/// </summary>
internal static class NavIcons
{
    private const float Grid = 24f;
    private const float StrokeWidth = 1.8f;

    // Each icon's outlines, and the parts filled in solid (the power profiles' slider knobs).
    private static readonly Dictionary<NavIcon, (GraphicsPath Stroke, GraphicsPath? Fill)> Shapes = new()
    {
        // A gauge: a dial, its needle and the arc of its scale.
        [NavIcon.Performance] = (SvgPath.Parse(Circle(12, 13, 8) + "M12 13l4-4M8 17a5 5 0 0 1 1-6"), null),

        // A sun.
        [NavIcon.Lighting] = (SvgPath.Parse(Circle(12, 12, 4) + "M12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M5 19l2-2M17 7l2-2"), null),

        // A monitor on its stand.
        [NavIcon.Display] = (SvgPath.Parse(Rect(3, 4, 18, 12, 1.5f) + "M8 20h8M12 16v4"), null),

        // A speaker and two sound waves.
        [NavIcon.Audio] = (SvgPath.Parse("M11 5 6 9H3v6h3l5 4zM15.5 8.5a5 5 0 0 1 0 7M18.5 5.5a9 9 0 0 1 0 13"), null),

        // Three sliders, their knobs at different heights.
        [NavIcon.PowerProfiles] = (SvgPath.Parse("M5 4v16M12 4v16M19 4v16" + Rect(3, 7, 4, 3) + Rect(10, 13, 4, 3) + Rect(17, 9, 4, 3)),
            SvgPath.Parse(Rect(3, 7, 4, 3) + Rect(10, 13, 4, 3) + Rect(17, 9, 4, 3))),

        // A wrench.
        [NavIcon.Optimize] = (SvgPath.Parse("M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18l3 3 6.3-6.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2.4-.6-.6-2.4z"), null),

        // A chip with its pins.
        [NavIcon.System] = (SvgPath.Parse(Rect(6, 6, 12, 12, 1.5f) + "M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"), null),

        // A gear.
        [NavIcon.Settings] = (SvgPath.Parse(Circle(12, 12, 3) +
            "M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"), null),

        // Three lines, for opening and closing the menu.
        [NavIcon.Menu] = (SvgPath.Parse("M3 6h18M3 12h18M3 18h18"), null),

        // A chip with its pins.
        [NavIcon.Cpu] = (SvgPath.Parse(Rect(6, 6, 12, 12, 1.5f) + "M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"), null),

        // A graphics card: its two fans and the pins below.
        [NavIcon.Gpu] = (SvgPath.Parse(Rect(2, 6, 20, 11, 1.5f) + Circle(8, 11.5f, 2.5f) + Circle(16, 11.5f, 2.5f) + "M5 17v3M9 17v3"), null),

        // A battery part full.
        [NavIcon.ChargeLimit] = (SvgPath.Parse(Rect(2, 7, 17, 10, 2) + "M22 11v2"), SvgPath.Parse(Rect(5, 10, 9, 4))),

        // A fan's three blades round its hub.
        [NavIcon.Fan] = (SvgPath.Parse(Circle(12, 12, 2) + "M12 3c2.5 0 3.5 2 2.6 4.4L13 10M21 15.5c-1.3 2.1-3.6 2.4-5.2.5L14 13.5M3 15.5c-1.2-1.8.2-3.7 2.7-4H9"), null),

        // A speaker.
        [NavIcon.Speaker] = (SvgPath.Parse("M11 5 6 9H3v6h3l5 4z"), null),

        // A plug.
        [NavIcon.Plug] = (SvgPath.Parse("M9 2v5M15 2v5M7 7h10v4a5 5 0 0 1-10 0zM12 16v6"), null),

        // A battery.
        [NavIcon.Battery] = (SvgPath.Parse(Rect(2, 7, 17, 10, 2) + "M22 11v2"), null),

        // A memory stick.
        [NavIcon.Memory] = (SvgPath.Parse(Rect(3, 7, 18, 10, 1) + "M7 7v10M11 7v10M15 7v10"), null),

        // A bin.
        [NavIcon.Trash] = (SvgPath.Parse("M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"), null),

        // A graphics card, for its shader cache.
        [NavIcon.ShaderCache] = (SvgPath.Parse(Rect(2, 6, 20, 11, 1.5f) + Circle(8, 11.5f, 2.5f) + Circle(16, 11.5f, 2.5f)), null),

        // A chip with a square at its heart, for the graphics chip.
        [NavIcon.FreeGpu] = (SvgPath.Parse(Rect(6, 6, 12, 12, 1.5f) + Rect(9.5f, 9.5f, 5, 5) + "M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"), null),

        // A crescent moon, for hibernation.
        [NavIcon.Moon] = (SvgPath.Parse("M12 3a9 9 0 1 0 9 9 7 7 0 0 1-9-9z"), null),

        // A pulse across a line, for the readings as they come.
        [NavIcon.Monitor] = (SvgPath.Parse("M2 12h4l3-7 5 14 3-7h5"), null),

        // A painter's palette with three dabs of color.
        [NavIcon.Palette] = (SvgPath.Parse("M12 3a9 9 0 1 0 0 18c1.1 0 1.8-.8 1.8-1.8 0-.5-.2-.9-.5-1.2-.3-.4-.5-.8-.5-1.3 0-1 .8-1.8 1.8-1.8H17a4 4 0 0 0 4-4c0-4.4-4-7.9-9-7.9z" + Circle(7.5f, 11, 1.2f) + Circle(10.5f, 7, 1.2f) + Circle(15.5f, 8, 1.2f)), null),

        // An arrow coming round, for how often the screen redraws.
        [NavIcon.RefreshRate] = (SvgPath.Parse("M20 12a8 8 0 1 1-2.4-5.7M20 4v4.5h-4.5"), null),

        // A keyboard: its outline, a row of keys and the space bar.
        [NavIcon.Keyboard] = (SvgPath.Parse(Rect(2, 6, 20, 12, 2) + "M6 10h.01M10 10h.01M14 10h.01M18 10h.01M8 14h8"), null),

        // A four-pointed spark, for the lit logo.
        [NavIcon.Logo] = (SvgPath.Parse("M12 3l2.2 6.8L21 12l-6.8 2.2L12 21l-2.2-6.8L3 12l6.8-2.2z"), null),

        // An equalizer's bars at different heights.
        [NavIcon.Equalizer] = (SvgPath.Parse("M4 20v-7M8 20V9M12 20V4M16 20v-9M20 20v-5"), null)
    };

    public static void Draw(Graphics graphics, NavIcon icon, RectangleF bounds, Color color)
    {
        var state = graphics.Save();

        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / Grid, bounds.Height / Grid);

            var (stroke, fill) = Shapes[icon];

            if (fill is not null)
            {
                using var brush = new SolidBrush(color);
                graphics.FillPath(brush, fill);
            }

            using var pen = new Pen(color, StrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.DrawPath(pen, stroke);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // A circle as path data: two half arcs.
    private static string Circle(float x, float y, float radius) =>
        Invariant($"M{x - radius} {y}a{radius} {radius} 0 1 0 {2 * radius} 0a{radius} {radius} 0 1 0 {-2 * radius} 0z");

    // A rectangle as path data, its corners rounded by <paramref name="radius"/>.
    private static string Rect(float x, float y, float width, float height, float radius = 0) => radius <= 0
        ? Invariant($"M{x} {y}h{width}v{height}h{-width}z")
        : Invariant($"M{x + radius} {y}h{width - 2 * radius}a{radius} {radius} 0 0 1 {radius} {radius}v{height - 2 * radius}a{radius} {radius} 0 0 1 {-radius} {radius}h{-(width - 2 * radius)}a{radius} {radius} 0 0 1 {-radius} {-radius}v{-(height - 2 * radius)}a{radius} {radius} 0 0 1 {radius} {-radius}z");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
