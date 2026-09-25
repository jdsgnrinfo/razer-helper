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

    // Window.
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
                case Glyph.Performance:
                    DrawRocket(graphics, pen);
                    break;
                case Glyph.Fans:
                    DrawFan(graphics, pen, brush);
                    break;
                case Glyph.Battery:
                    DrawBattery(graphics, pen, brush);
                    break;
                case Glyph.Lighting:
                    DrawBulb(graphics, pen);
                    break;
                case Glyph.Display:
                    DrawMonitor(graphics, pen);
                    break;
                case Glyph.Balanced:
                    DrawGauge(graphics, pen, brush);
                    break;
                case Glyph.Silent:
                    DrawLeaf(graphics, pen);
                    break;
                case Glyph.Gaming:
                    DrawBolt(graphics, brush);
                    break;
                case Glyph.Custom:
                    DrawSliders(graphics, pen, brush);
                    break;
                case Glyph.Cpu:
                    DrawChip(graphics, pen, brush);
                    break;
                case Glyph.Gpu:
                    DrawGraphicsCard(graphics, pen);
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

    // Balanced: a speedometer, an open dial with its needle part way up.
    private static void DrawGauge(Graphics graphics, Pen pen, Brush brush)
    {
        graphics.DrawArc(pen, 1.5f, 2.5f, 13f, 13f, 150, 240);
        graphics.DrawLine(pen, 8f, 9f, 11.6f, 5.4f);
        graphics.FillEllipse(brush, 6.6f, 7.6f, 2.8f, 2.8f);
    }

    // Three curved blades around a hub.
    private static void DrawFan(Graphics graphics, Pen pen, Brush brush)
    {
        for (var blade = 0; blade < 3; blade++)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(8f, 8f);
            graphics.RotateTransform(blade * 120f);

            using var path = new GraphicsPath();
            path.AddBezier(0f, -1.5f, 1.5f, -4.5f, 4.5f, -6.5f, 3f, -7f);
            path.AddBezier(3f, -7f, 0.5f, -7.5f, -2f, -5f, -1.2f, -1.2f);
            path.CloseFigure();
            graphics.FillPath(brush, path);

            graphics.Restore(state);
        }

        graphics.FillEllipse(brush, 6.3f, 6.3f, 3.4f, 3.4f);
    }

    // A battery on its side, two thirds full.
    private static void DrawBattery(Graphics graphics, Pen pen, Brush brush)
    {
        using (var body = RoundedButton.RoundedPath(new RectangleF(1f, 4.5f, 12.5f, 7f), 1.5f))
            graphics.DrawPath(pen, body);

        graphics.FillRectangle(brush, 14.3f, 6.5f, 1.4f, 3f);
        graphics.FillRectangle(brush, 3f, 6.5f, 6.5f, 3f);
    }

    // A monitor on its stand.
    private static void DrawMonitor(Graphics graphics, Pen pen)
    {
        using (var screen = RoundedButton.RoundedPath(new RectangleF(1.5f, 2f, 13f, 9f), 1.5f))
            graphics.DrawPath(pen, screen);

        graphics.DrawLine(pen, 8f, 11f, 8f, 14f);
        graphics.DrawLine(pen, 5f, 14f, 11f, 14f);
    }

    // Performance (the section): a rocket going up, with its window, two fins
    // and a short exhaust.
    private static void DrawRocket(Graphics graphics, Pen pen)
    {
        using (var body = new GraphicsPath())
        {
            body.AddBezier(8f, 1f, 10.8f, 3.2f, 11f, 7f, 10.6f, 11f);
            body.AddLine(10.6f, 11f, 5.4f, 11f);
            body.AddBezier(5.4f, 11f, 5f, 7f, 5.2f, 3.2f, 8f, 1f);
            body.CloseFigure();
            graphics.DrawPath(pen, body);
        }

        graphics.DrawEllipse(pen, 6.7f, 4.6f, 2.6f, 2.6f);

        graphics.DrawLines(pen, [new PointF(5.3f, 7.8f), new PointF(2.8f, 10.4f), new PointF(2.8f, 12.4f), new PointF(5.4f, 11f)]);
        graphics.DrawLines(pen, [new PointF(10.7f, 7.8f), new PointF(13.2f, 10.4f), new PointF(13.2f, 12.4f), new PointF(10.6f, 11f)]);

        graphics.DrawLine(pen, 8f, 13f, 8f, 15f);
    }

    // Silent: a leaf, for going easy, with its midrib and stem.
    private static void DrawLeaf(Graphics graphics, Pen pen)
    {
        using (var leaf = new GraphicsPath())
        {
            leaf.AddBezier(3.5f, 12.5f, 2.8f, 6.2f, 7f, 2.5f, 14f, 2f);
            leaf.AddBezier(14f, 2f, 13.8f, 9f, 10f, 13.2f, 3.5f, 12.5f);
            leaf.CloseFigure();
            graphics.DrawPath(pen, leaf);
        }

        graphics.DrawLine(pen, 3.5f, 12.5f, 10.5f, 5.5f);
        graphics.DrawLine(pen, 3.5f, 12.5f, 1.5f, 14.5f);
    }

    // Gaming: a lightning bolt, for the extra power.
    private static void DrawBolt(Graphics graphics, Brush brush)
    {
        graphics.FillPolygon(brush,
        [
            new PointF(9.8f, 1f),
            new PointF(3.2f, 9.2f),
            new PointF(7.4f, 9.2f),
            new PointF(6.2f, 15f),
            new PointF(12.8f, 6.8f),
            new PointF(8.6f, 6.8f)
        ]);
    }

    // Custom: three sliders, each set differently.
    private static void DrawSliders(Graphics graphics, Pen pen, Brush brush)
    {
        (float Y, float Knob)[] sliders = [(3.5f, 10.5f), (8f, 5f), (12.5f, 11.5f)];

        foreach (var (y, knob) in sliders)
        {
            graphics.DrawLine(pen, 2f, y, 14f, y);
            graphics.FillEllipse(brush, knob - 2f, y - 2f, 4f, 4f);
        }
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

    // A light bulb with its screw base.
    private static void DrawBulb(Graphics graphics, Pen pen)
    {
        using var glass = new GraphicsPath();
        glass.AddArc(3f, 1f, 10f, 10f, 140, 260);
        glass.AddLine(11.8f, 9.4f, 10.2f, 11.5f);
        glass.AddLine(5.8f, 11.5f, 4.2f, 9.4f);
        glass.CloseFigure();
        graphics.DrawPath(pen, glass);

        graphics.DrawLine(pen, 6f, 13.2f, 10f, 13.2f);
        graphics.DrawLine(pen, 6.8f, 15f, 9.2f, 15f);
    }
}
