using System.Drawing.Drawing2D;

namespace RazerHelper.UI;

/// <summary>The app's small pictures: before a section's title, on the performance mode buttons, and the window's close button.</summary>
internal enum Glyph
{
    // Sections.
    Performance,
    Fans,
    Battery,
    Lighting,
    Display,

    // Performance modes.
    Balanced,
    Silent,
    Gaming,
    Custom,

    // Custom mode's boost selectors.
    Cpu,
    Gpu,

    // Windows.
    SystemInfo,
    Settings,
    Close,

    // Footer and header actions.
    Cleaning,
    Leave
}

/// <summary>
/// Draws the icons as vector shapes on a 16 by 16 design grid, scaled
/// to whatever box they are given, so they stay sharp at every display scale
/// and need no image files or icon font.
/// </summary>
internal static class Glyphs
{
    private const float Grid = 16f;

    /// <summary>
    /// A soft halo of <paramref name="color"/> around the icon, drawn before
    /// it: faint copies of the icon spread in rings around its place, which
    /// pile up into a glow that fades outward.
    /// </summary>
    public static void DrawGlow(Graphics graphics, Glyph icon, RectangleF bounds, Color color, float radius, float opacity = 1)
    {
        const int Directions = 16;
        const int Rings = 6;

        for (var ring = Rings; ring >= 1; ring--)
        {
            var distance = radius * ring / Rings;
            // Fainter the farther out, so the glow blurs away instead of ending in an edge.
            var alpha = (int)Math.Round((4 + 8f * (Rings - ring) / (Rings - 1)) * opacity);

            if (alpha <= 0)
                continue;

            var faint = Color.FromArgb(alpha, color);

            for (var step = 0; step < Directions; step++)
            {
                var angle = step * 2 * Math.PI / Directions;
                var offset = new SizeF((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
                Draw(graphics, icon, new RectangleF(bounds.Location + offset, bounds.Size), faint);
            }
        }
    }

    public static void Draw(Graphics graphics, Glyph icon, RectangleF bounds, Color color)
    {
        var state = graphics.Save();

        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / Grid, bounds.Height / Grid);

            using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);

            switch (icon)
            {
                case Glyph.Performance:
                    FillSetIcon(graphics, IconSet.Rocket, brush);
                    break;
                case Glyph.Fans:
                    FillSetIcon(graphics, IconSet.Fan, brush);
                    break;
                case Glyph.Battery:
                    FillSetIcon(graphics, IconSet.BatteryCharging, brush);
                    break;
                case Glyph.Lighting:
                    FillSetIcon(graphics, IconSet.Twilight, brush);
                    break;
                case Glyph.Display:
                    FillSetIcon(graphics, IconSet.Display, brush);
                    break;
                case Glyph.Balanced:
                    DrawGauge(graphics, pen, brush);
                    break;
                case Glyph.Silent:
                    FillSetIcon(graphics, IconSet.Leaf, brush);
                    break;
                case Glyph.Gaming:
                    FillSetIcon(graphics, IconSet.GameController, brush);
                    break;
                case Glyph.Custom:
                    FillSetIcon(graphics, IconSet.Widget, brush);
                    break;
                case Glyph.Cpu:
                    DrawChip(graphics, pen, brush);
                    break;
                case Glyph.Gpu:
                    DrawGraphicsCard(graphics, pen);
                    break;
                case Glyph.SystemInfo:
                    FillSetIcon(graphics, IconSet.Laptop, brush);
                    break;
                case Glyph.Settings:
                    FillSetIcon(graphics, IconSet.Cog, brush);
                    break;
                case Glyph.Cleaning:
                    FillSetIcon(graphics, IconSet.Broom, brush);
                    break;
                case Glyph.Leave:
                    FillSetIcon(graphics, IconSet.ArrowOutOfBox, brush);
                    break;
                case Glyph.Close:
                    // An X, in the same stroke as the outlined icons.
                    graphics.DrawLine(pen, 3.5f, 3.5f, 12.5f, 12.5f);
                    graphics.DrawLine(pen, 12.5f, 3.5f, 3.5f, 12.5f);
                    break;
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // Icons taken from icon sets, as react-icons ships them, each drawn on its
    // own grid and scaled onto ours.
    private static void FillSetIcon(Graphics graphics, SetIcon icon, Brush brush)
    {
        var state = graphics.Save();
        graphics.ScaleTransform(Grid / icon.Grid, Grid / icon.Grid);
        graphics.FillPath(brush, icon.Path);
        graphics.Restore(state);
    }

    private sealed record SetIcon(float Grid, GraphicsPath Path)
    {
        public SetIcon(float grid, string pathData) : this(grid, SvgPath.Parse(pathData))
        {
        }
    }

    private static class IconSet
    {
        // Performance (the section): BiSolidRocket (Boxicons).
        public static readonly SetIcon Rocket = new(24f,
            "M15.78 15.84S18.64 13 19.61 12c3.07-3 1.54-9.18 1.54-9.18S15 1.29 12 4.36C9.66 6.64 8.14 8.22 8.14 8.22S4.3 7.42 2 9.72L14.25 22c2.3-2.33 1.53-6.16 1.53-6.16zm-1.5-9a2 2 0 0 1 2.83 0 2 2 0 1 1-2.83 0zM3 21a7.81 7.81 0 0 0 5-2l-3-3c-2 1-2 5-2 5z");

        // Fans: PiFanFill (Phosphor).
        public static readonly SetIcon Fan = new(256f,
            "M233,135a60,60,0,0,0-89.62-35.45l16.39-65.44a8,8,0,0,0-3.45-8.68A60,60,0,1,0,95.69,128.91L30.82,147.44a8,8,0,0,0-5.8,7.32,60,60,0,0,0,44.42,60.66,60.52,60.52,0,0,0,15.62,2.07,60.07,60.07,0,0,0,59.88-62l48.48,46.92a8,8,0,0,0,9.25,1.35A60,60,0,0,0,233,135ZM130.44,147.85a20,20,0,1,1,17.41-22.29A20,20,0,0,1,130.44,147.85Z");

        // Battery charge limit: PiBatteryChargingFill (Phosphor).
        public static readonly SetIcon BatteryCharging = new(256f,
            "M256,96v64a8,8,0,0,1-16,0V96a8,8,0,0,1,16,0ZM224,80v96a24,24,0,0,1-24,24H32A24,24,0,0,1,8,176V80A24,24,0,0,1,32,56H200A24,24,0,0,1,224,80Zm-85.19,43.79A8,8,0,0,0,132,120H112.94l10.22-20.42a8,8,0,1,0-14.32-7.16l-16,32A8,8,0,0,0,100,136h19.06l-10.22,20.42a8,8,0,0,0,14.32,7.16l16-32A8,8,0,0,0,138.81,123.79Z");

        // Lighting: MdOutlineWbTwilight (Material Icons, outlined).
        public static readonly SetIcon Twilight = new(24f,
            "m16.955 8.662 2.12-2.122 1.416 1.414-2.121 2.122zM2 18h20v2H2zm9-14h2v3h-2zM3.543 7.925 4.957 6.51l2.121 2.12-1.414 1.415zM5 16h14c0-3.87-3.13-7-7-7s-7 3.13-7 7z");

        // Display: PiFrameCornersFill (Phosphor).
        public static readonly SetIcon Display = new(256f,
            "M216,40H40A16,16,0,0,0,24,56V200a16,16,0,0,0,16,16H216a16,16,0,0,0,16-16V56A16,16,0,0,0,216,40ZM88,192H56a8,8,0,0,1-8-8V152a8,8,0,0,1,16,0v24H88a8,8,0,0,1,0,16Zm120-88a8,8,0,0,1-16,0V80H168a8,8,0,0,1,0-16h32a8,8,0,0,1,8,8Z");

        // Silent: BiSolidLeaf (Boxicons).
        public static readonly SetIcon Leaf = new(24f,
            "m22 3.41-.12-1.26-1.2.4a13.84 13.84 0 0 1-6.41.64 11.87 11.87 0 0 0-6.68.9A7.23 7.23 0 0 0 3.3 9.5a9 9 0 0 0 .39 4.58 16.6 16.6 0 0 1 1.18-2.2 9.85 9.85 0 0 1 4.07-3.43 11.16 11.16 0 0 1 5.06-1A12.08 12.08 0 0 0 9.34 9.2a9.48 9.48 0 0 0-1.86 1.53 11.38 11.38 0 0 0-1.39 1.91 16.39 16.39 0 0 0-1.57 4.54A26.42 26.42 0 0 0 4 22h2a30.69 30.69 0 0 1 .59-4.32 9.25 9.25 0 0 0 4.52 1.11 11 11 0 0 0 4.28-.87C23 14.67 22 3.86 22 3.41z");

        // Gaming: IoGameController (Ionicons 5).
        public static readonly SetIcon GameController = new(512f,
            "M483.13 245.38C461.92 149.49 430 98.31 382.65 84.33A107.13 107.13 0 00352 80c-13.71 0-25.65 3.34-38.28 6.88C298.5 91.15 281.21 96 256 96s-42.51-4.84-57.76-9.11C185.6 83.34 173.67 80 160 80a115.74 115.74 0 00-31.73 4.32c-47.1 13.92-79 65.08-100.52 161C4.61 348.54 16 413.71 59.69 428.83a56.62 56.62 0 0018.64 3.22c29.93 0 53.93-24.93 70.33-45.34 18.53-23.1 40.22-34.82 107.34-34.82 59.95 0 84.76 8.13 106.19 34.82 13.47 16.78 26.2 28.52 38.9 35.91 16.89 9.82 33.77 12 50.16 6.37 25.82-8.81 40.62-32.1 44-69.24 2.57-28.48-1.39-65.89-12.12-114.37zM208 240h-32v32a16 16 0 01-32 0v-32h-32a16 16 0 010-32h32v-32a16 16 0 0132 0v32h32a16 16 0 010 32zm84 4a20 20 0 1120-20 20 20 0 01-20 20zm44 44a20 20 0 1120-19.95A20 20 0 01336 288zm0-88a20 20 0 1120-20 20 20 0 01-20 20zm44 44a20 20 0 1120-20 20 20 0 01-20 20z");

        // Custom: BiSolidWidget (Boxicons).
        public static readonly SetIcon Widget = new(24f,
            "M4 11h6a1 1 0 0 0 1-1V4a1 1 0 0 0-1-1H4a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1zm0 10h6a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1H4a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1zm10 0h6a1 1 0 0 0 1-1v-6a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1zm7.293-14.707-3.586-3.586a.999.999 0 0 0-1.414 0l-3.586 3.586a.999.999 0 0 0 0 1.414l3.586 3.586a.999.999 0 0 0 1.414 0l3.586-3.586a.999.999 0 0 0 0-1.414z");

        // System information: PiLaptopFill (Phosphor).
        public static readonly SetIcon Laptop = new(256f,
            "M232,168h-8V72a24,24,0,0,0-24-24H56A24,24,0,0,0,32,72v96H24a8,8,0,0,0-8,8v16a24,24,0,0,0,24,24H216a24,24,0,0,0,24-24V176A8,8,0,0,0,232,168ZM112,72h32a8,8,0,0,1,0,16H112a8,8,0,0,1,0-16ZM224,192a8,8,0,0,1-8,8H40a8,8,0,0,1-8-8v-8H224Z");

        // Settings: BiSolidCog (Boxicons).
        public static readonly SetIcon Cog = new(24f,
            "m2.344 15.271 2 3.46a1 1 0 0 0 1.366.365l1.396-.806c.58.457 1.221.832 1.895 1.112V21a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-1.598a8.094 8.094 0 0 0 1.895-1.112l1.396.806c.477.275 1.091.11 1.366-.365l2-3.46a1.004 1.004 0 0 0-.365-1.366l-1.372-.793a7.683 7.683 0 0 0-.002-2.224l1.372-.793c.476-.275.641-.89.365-1.366l-2-3.46a1 1 0 0 0-1.366-.365l-1.396.806A8.034 8.034 0 0 0 15 4.598V3a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v1.598A8.094 8.094 0 0 0 7.105 5.71L5.71 4.904a.999.999 0 0 0-1.366.365l-2 3.46a1.004 1.004 0 0 0 .365 1.366l1.372.793a7.683 7.683 0 0 0 0 2.224l-1.372.793c-.476.275-.641.89-.365 1.366zM12 8c2.206 0 4 1.794 4 4s-1.794 4-4 4-4-1.794-4-4 1.794-4 4-4z");

        // Free up GPU: MdCleaningServices (Material Icons).
        public static readonly SetIcon Broom = new(24f,
            "M16 11h-1V3c0-1.1-.9-2-2-2h-2c-1.1 0-2 .9-2 2v8H8c-2.76 0-5 2.24-5 5v7h18v-7c0-2.76-2.24-5-5-5m3 10h-2v-3c0-.55-.45-1-1-1s-1 .45-1 1v3h-2v-3c0-.55-.45-1-1-1s-1 .45-1 1v3H9v-3c0-.55-.45-1-1-1s-1 .45-1 1v3H5v-5c0-1.65 1.35-3 3-3h8c1.65 0 3 1.35 3 3z");

        // Close: HiMiniArrowRightStartOnRectangle (Heroicons, mini).
        public static readonly SetIcon ArrowOutOfBox = new(20f,
            "M3 4.25A2.25 2.25 0 0 1 5.25 2h5.5A2.25 2.25 0 0 1 13 4.25v2a.75.75 0 0 1-1.5 0v-2a.75.75 0 0 0-.75-.75h-5.5a.75.75 0 0 0-.75.75v11.5c0 .414.336.75.75.75h5.5a.75.75 0 0 0 .75-.75v-2a.75.75 0 0 1 1.5 0v2A2.25 2.25 0 0 1 10.75 18h-5.5A2.25 2.25 0 0 1 3 15.75V4.25Z" +
            "M6 10a.75.75 0 0 1 .75-.75h9.546l-1.048-.943a.75.75 0 1 1 1.004-1.114l2.5 2.25a.75.75 0 0 1 0 1.114l-2.5 2.25a.75.75 0 1 1-1.004-1.114l1.048-.943H6.75A.75.75 0 0 1 6 10Z");
    }

    // Balanced: a speedometer, an open dial with its needle part way up.
    private static void DrawGauge(Graphics graphics, Pen pen, Brush brush)
    {
        graphics.DrawArc(pen, 1.5f, 2.5f, 13f, 13f, 150, 240);
        graphics.DrawLine(pen, 8f, 9f, 11.6f, 5.4f);
        graphics.FillEllipse(brush, 6.6f, 7.6f, 2.8f, 2.8f);
    }

    // CPU: a chip, its die in the middle and three pins on every side.
    private static void DrawChip(Graphics graphics, Pen pen, Brush brush)
    {
        using (var body = RoundedButton.RoundedPath(new RectangleF(4f, 4f, 8f, 8f), 1.2f))
            graphics.DrawPath(pen, body);

        graphics.FillRectangle(brush, 6.6f, 6.6f, 2.8f, 2.8f);

        foreach (var at in new[] { 6f, 8f, 10f })
        {
            graphics.DrawLine(pen, at, 1.5f, at, 4f);
            graphics.DrawLine(pen, at, 12f, at, 14.5f);
            graphics.DrawLine(pen, 1.5f, at, 4f, at);
            graphics.DrawLine(pen, 12f, at, 14.5f, at);
        }
    }

    // GPU: a graphics card seen from the side, its fan, a heatsink and the
    // slot connector along the bottom.
    private static void DrawGraphicsCard(Graphics graphics, Pen pen)
    {
        using (var board = RoundedButton.RoundedPath(new RectangleF(1.5f, 3.5f, 13f, 8f), 1.5f))
            graphics.DrawPath(pen, board);

        graphics.DrawEllipse(pen, 3.7f, 5.3f, 4.4f, 4.4f);
        graphics.DrawLine(pen, 10.3f, 5.8f, 10.3f, 9.2f);
        graphics.DrawLine(pen, 12.3f, 5.8f, 12.3f, 9.2f);

        graphics.DrawLines(pen, [new PointF(4f, 11.5f), new PointF(4f, 13.8f), new PointF(10f, 13.8f), new PointF(10f, 11.5f)]);
    }
}
