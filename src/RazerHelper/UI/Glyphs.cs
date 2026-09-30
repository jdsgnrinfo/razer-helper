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
    Close
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
        // Silent: PiFanFill (Phosphor).
        public static readonly SetIcon Fan = new(256f,
            "M233,135a60,60,0,0,0-89.62-35.45l16.39-65.44a8,8,0,0,0-3.45-8.68A60,60,0,1,0,95.69,128.91L30.82,147.44a8,8,0,0,0-5.8,7.32,60,60,0,0,0,44.42,60.66,60.52,60.52,0,0,0,15.62,2.07,60.07,60.07,0,0,0,59.88-62l48.48,46.92a8,8,0,0,0,9.25,1.35A60,60,0,0,0,233,135ZM130.44,147.85a20,20,0,1,1,17.41-22.29A20,20,0,0,1,130.44,147.85Z");

        // Balanced: MdSpeed (Material Icons).
        public static readonly SetIcon Speedometer = new(24f,
            "m20.38 8.57-1.23 1.85a8 8 0 0 1-.22 7.58H5.07A8 8 0 0 1 15.58 6.85l1.85-1.23A10 10 0 0 0 3.35 19a2 2 0 0 0 1.72 1h13.85a2 2 0 0 0 1.74-1 10 10 0 0 0-.27-10.44zm-9.79 6.84a2 2 0 0 0 2.83 0l5.66-8.49-8.49 5.66a2 2 0 0 0 0 2.83");

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
