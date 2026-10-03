using System.Drawing.Drawing2D;

namespace RazerHelper.UI;

/// <summary>The app's small pictures: on the performance mode buttons, before the CPU and GPU titles, and the windows' close button.</summary>
internal enum Glyph
{
    // Performance modes.
    Balanced,
    Silent,
    Custom,

    // Custom mode's boost selectors.
    Cpu,
    Gpu,

    // Windows.
    Close,

    // The sections in the window's sidebar.
    Rocket,
    Display,
    Battery,
    System,
    Optimize,
    Settings
}

/// <summary>
/// Draws the icons as vector shapes on a 16 by 16 design grid, scaled
/// to whatever box they are given, so they stay sharp at every display scale
/// and need no image files or icon font.
/// </summary>
internal static class Glyphs
{
    private const float Grid = 16f;

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
                case Glyph.Balanced:
                    FillSetIcon(graphics, IconSet.Speedometer, brush);
                    break;
                case Glyph.Silent:
                    FillSetIcon(graphics, IconSet.Fan, brush);
                    break;
                case Glyph.Custom:
                    FillSetIcon(graphics, IconSet.Tune, brush);
                    break;
                case Glyph.Cpu:
                    FillSetIcon(graphics, IconSet.Chip, brush);
                    break;
                case Glyph.Gpu:
                    FillSetIcon(graphics, IconSet.GraphicsCard, brush);
                    break;
                case Glyph.Close:
                    // An X, in the same stroke as the outlined icons.
                    graphics.DrawLine(pen, 3.5f, 3.5f, 12.5f, 12.5f);
                    graphics.DrawLine(pen, 12.5f, 3.5f, 3.5f, 12.5f);
                    break;
                case Glyph.Rocket:
                    StrokeSetIcon(graphics, IconSet.Rocket, color);
                    break;
                case Glyph.Display:
                    StrokeSetIcon(graphics, IconSet.Display, color);
                    break;
                case Glyph.Battery:
                    StrokeSetIcon(graphics, IconSet.Battery, color);
                    break;
                case Glyph.System:
                    StrokeSetIcon(graphics, IconSet.Processor, color);
                    break;
                case Glyph.Optimize:
                    StrokeSetIcon(graphics, IconSet.Bolt, color);
                    break;
                case Glyph.Settings:
                    StrokeSetIcon(graphics, IconSet.Gear, color);
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

    // Outlined icons, drawn as lines 2 units wide on their own grid, as they
    // are in the SVG they come from (stroke-width 2, round ends and joins).
    private static void StrokeSetIcon(Graphics graphics, SetIcon icon, Color color)
    {
        var state = graphics.Save();
        graphics.ScaleTransform(Grid / icon.Grid, Grid / icon.Grid);
        using var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawPath(pen, icon.Path);
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
        // The sidebar's sections, outlined on a 24 grid. Performance: Rocket (Lucide).
        public static readonly SetIcon Rocket = new(24f,
            "M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z" +
            "M12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" +
            "M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0" +
            "M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5");

        // Display and lighting: a screen on its stand, designed for this fork.
        public static readonly SetIcon Display = new(24f,
            "M4 4h16a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z" +
            "M8 20h8M12 16v4");

        // Energy: a battery three bars full, designed for this fork.
        public static readonly SetIcon Battery = new(24f,
            "M5 7h12a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2z" +
            "M21 11v2M6 10v4M9 10v4M12 10v4");

        // System: a chip with its pins, designed for this fork.
        public static readonly SetIcon Processor = new(24f,
            "M7 6h10a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1z" +
            "M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4");

        // Optimize: a lightning bolt, designed for this fork.
        public static readonly SetIcon Bolt = new(24f, "M13 3L4 14h7l-1 7 9-11h-7z");

        // Settings: Settings, a gear (Lucide).
        public static readonly SetIcon Gear = new(24f,
            "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z" +
            "M15 12a3 3 0 1 1-6 0a3 3 0 1 1 6 0z");

        // Silent: a fan, designed for this fork (its three blades, then the hub).
        public static readonly SetIcon Fan = new(157.97f,
            "M70.66,56.52c16.78-6.15,33.31,3.55,37.82,20.84,8.81-5.81,14.66-13.35,18.71-22.2,5.04-12.02,4.03-25.25-3.31-36.08C101.86-13.43,52.4,3.17,50.35,13.83c-.5,2.59.19,4.92,1.68,7.15,7.55,11.25,13.8,22.72,18.62,35.54Z" +
            "M60.15,63.94c-12.38-8.39-27.29-9.43-40.5-2.23C8.15,67.98.27,80.33,0,95.19c-.34,19.13,9.5,37.6,24.76,48.85,2.42,1.78,4.82,2.52,7.57,2.03,1.69-.3,3.53-1.87,5.03-4.32,7.88-12.83,15.71-24.7,24.75-36-13.1-11.26-13.74-29.75-1.97-41.82Z" +
            "M156.12,106.43c-1.33-2.2-3.68-2.57-6.42-3.02-14.41-2.38-27.91-6.75-41.54-12.42-4.18,16.05-20.11,24.87-35.79,20.09-1.38,10.41-.11,20.38,5.25,29.26,9.02,14.94,26.41,21.7,43.66,15.12,18.46-7.04,31.67-23.34,36.11-41.89.68-2.85-.16-5.28-1.28-7.13Z" +
            "M63.79,83.5a16.89,16.89,0,1,0,33.78,0a16.89,16.89,0,1,0,-33.78,0Z");

        // Balanced: a gauge in a disc, designed for this fork.
        public static readonly SetIcon Speedometer = new(157.97f,
            "M78.99,0C35.36,0,0,35.36,0,78.99s35.36,78.99,78.99,78.99,78.99-35.36,78.99-78.99S122.61,0,78.99,0ZM30.75,75.38c-.52,9.91,2.33,19.62,8.27,28.08.99,1.41,3.99,5.69-.06,9.59-1.21,1.17-2.82,1.81-4.52,1.81-.19,0-.39,0-.58-.03-1.95-.17-3.75-1.21-4.94-2.84-10.74-14.78-14.02-33.43-8.99-51.17C29.04,28.69,62.69,9.62,94.94,18.32c1.32.36,2.47,1.36,3.23,2.82.88,1.68,1.12,3.66.62,5.03-.76,2.11-3.84,5.46-7.27,4.61-14.54-3.58-29.64-.61-41.41,8.13-11.53,8.56-18.59,21.85-19.36,36.46ZM87.83,91.9c-2.61,1.72-5.67,2.63-8.83,2.63-4.15,0-8.06-1.54-11-4.34-5.53-5.26-6.6-13.67-2.55-20,4.06-6.33,11.62-9.12,18.56-6.94l32.92-30.58c.96-.89,2.24-1.23,3.59-.94,1.9.4,3.47,1.89,3.98,3.08.91,2.12,1.13,5.19-.97,7.47l-29.25,31.78c1.89,6.92-.66,14.03-6.46,17.84ZM128.59,112.59c-1.09,1.48-2.73,2.29-4.62,2.29-1.41,0-2.87-.45-4.01-1.23-2.44-1.67-3.32-4.56-2.43-7.94l.1-.38.24-.32c8.2-10.8,11.22-24.84,8.29-38.54-.7-3.27.72-6.22,3.7-7.7,1.49-.74,3.71-.68,5.53.14,1.58.72,2.67,1.95,3.07,3.46,4.54,17.31.95,35.61-9.86,50.21Z");

        // Custom: MdTune (Material Icons).
        public static readonly SetIcon Tune = new(24f,
            "M3 17v2h6v-2zM3 5v2h10V5zm10 16v-2h8v-2h-8v-2h-2v6zM7 9v2H3v2h4v2h2V9zm14 4v-2H11v2zm-6-4h2V7h4V5h-4V3h-2z");

        // Custom's CPU: PiCpuFill (Phosphor).
        public static readonly SetIcon Chip = new(256f,
            "M104,104h48v48H104Zm136,48a8,8,0,0,1-8,8H216v40a16,16,0,0,1-16,16H160v16a8,8,0,0,1-16,0V216H112v16a8,8,0,0,1-16,0V216H56a16,16,0,0,1-16-16V160H24a8,8,0,0,1,0-16H40V112H24a8,8,0,0,1,0-16H40V56A16,16,0,0,1,56,40H96V24a8,8,0,0,1,16,0V40h32V24a8,8,0,0,1,16,0V40h40a16,16,0,0,1,16,16V96h16a8,8,0,0,1,0,16H216v32h16A8,8,0,0,1,240,152ZM168,96a8,8,0,0,0-8-8H96a8,8,0,0,0-8,8v64a8,8,0,0,0,8,8h64a8,8,0,0,0,8-8Z");

        // Custom's GPU: PiGraphicsCard (Phosphor).
        public static readonly SetIcon GraphicsCard = new(256f,
            "M232,48H16a8,8,0,0,0-8,8V208a8,8,0,0,0,16,0V192H40v16a8,8,0,0,0,16,0V192H72v16a8,8,0,0,0,16,0V192h16v16a8,8,0,0,0,16,0V192H232a16,16,0,0,0,16-16V64A16,16,0,0,0,232,48Zm0,128H24V64H232Zm-56-16a40,40,0,1,0-40-40A40,40,0,0,0,176,160Zm-24-40a23.74,23.74,0,0,1,2.35-10.34l32,32A23.74,23.74,0,0,1,176,144,24,24,0,0,1,152,120Zm48,0a23.74,23.74,0,0,1-2.35,10.34l-32-32A23.74,23.74,0,0,1,176,96,24,24,0,0,1,200,120ZM80,160a40,40,0,1,0-40-40A40,40,0,0,0,80,160ZM56,120a23.74,23.74,0,0,1,2.35-10.34l32,32A23.74,23.74,0,0,1,80,144,24,24,0,0,1,56,120Zm48,0a23.74,23.74,0,0,1-2.35,10.34l-32-32A23.74,23.74,0,0,1,80,96,24,24,0,0,1,104,120Z");
    }
}
