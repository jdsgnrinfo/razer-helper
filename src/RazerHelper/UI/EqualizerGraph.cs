using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The equalizer: a slider standing up for each band, its gain written above
/// it and its frequency below, on a grid from +12 to -12 dB. A green curve
/// runs through the thumbs, softly glowing, with a faint green fill under it,
/// and moves with them. Drag a thumb (or anywhere in its column) to set the
/// band; the mouse wheel moves the band under the pointer a step, and a
/// double click puts it back to 0. Off, the curve and fills turn grey.
/// </summary>
/// <remarks>
/// <see cref="GainsChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> once the user lets go or the wheel stops.
/// </remarks>
internal sealed class EqualizerGraph : Control
{
    private static int ThumbHalf => S(8);
    private static int TrackWidth => S(5);
    private static int Corner => S(6);

    // The grid's top and bottom (+12 and -12 dB), and its first and last band.
    private int GridTop => S(38);
    private int GridBottom => Height - S(42);
    private int FirstX => S(58);
    private int LastX => Width - S(28);

    private static readonly Color PanelColor = SidebarColor;
    private static readonly Color MutedColor = OffColor;

    private readonly double[] _gains = EqBands.Flat();
    private int _dragging = -1;
    private int _hovered = -1;
    private readonly System.Windows.Forms.Timer _wheelSettle = new() { Interval = 400 };

    public EqualizerGraph()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = BackgroundColor;
        Cursor = Cursors.Hand;
        _wheelSettle.Tick += (_, _) =>
        {
            _wheelSettle.Stop();
            Committed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Fires on every step while a band moves.</summary>
    public event EventHandler? GainsChanged;

    /// <summary>Fires once a move is done.</summary>
    public event EventHandler? Committed;

    /// <summary>Whether the equalizer is on: green when it is, grey when not. It can be set either way.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Invalidate();
        }
    }

    private bool _active = true;

    /// <summary>The bands' gains, low to high, in dB.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<double> Gains => _gains;

    /// <summary>Shows a curve without raising any event.</summary>
    public void Show(IReadOnlyList<double> gains)
    {
        for (var band = 0; band < _gains.Length; band++)
            _gains[band] = band < gains.Count ? EqBands.Snap(gains[band]) : 0;

        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || BandAt(e.X) is not { } band)
            return;

        Focus();
        _dragging = band;
        SetGain(band, GainAt(e.Y));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging >= 0)
        {
            SetGain(_dragging, GainAt(e.Y));
            return;
        }

        var hovered = BandAt(e.X) ?? -1;

        if (hovered != _hovered)
        {
            _hovered = hovered;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_dragging < 0)
            return;

        _dragging = -1;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (BandAt(e.X) is { } band)
        {
            SetGain(band, 0);
            Committed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (BandAt(e.X) is not { } band)
            return;

        SetGain(band, _gains[band] + Math.Sign(e.Delta) * EqBands.Step);
        _wheelSettle.Stop();
        _wheelSettle.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var accent = _active ? RazerGreen : MutedColor;

        using (var panel = RoundedButton.RoundedPath(new RectangleF(0, 0, Width - 1, Height - 1), Corner))
        using (var fill = new SolidBrush(PanelColor))
            graphics.FillPath(fill, panel);

        PaintGrid(graphics);

        var points = _gains.Select((gain, band) => new PointF(XOf(band), YOf(gain))).ToArray();
        var zero = YOf(0);

        // The area under the curve, fading down to nothing.
        using (var curve = CurvePath(points))
        using (var area = (GraphicsPath)curve.Clone())
        {
            area.AddLine(points[^1], new PointF(points[^1].X, GridBottom));
            area.AddLine(new PointF(points[^1].X, GridBottom), new PointF(points[0].X, GridBottom));
            area.CloseFigure();

            using var shade = new LinearGradientBrush(
                new RectangleF(0, GridTop - 1, 1, GridBottom - GridTop + 2),
                Color.FromArgb(_active ? 70 : 40, accent),
                Color.FromArgb(0, accent),
                LinearGradientMode.Vertical);
            graphics.FillPath(shade, area);

            // The tracks and their fills, behind the curve.
            using var track = new SolidBrush(TrackColor);
            using var filled = new SolidBrush(accent);

            foreach (var point in points)
            {
                FillBar(graphics, track, point.X, GridTop, GridBottom);
                FillBar(graphics, filled, point.X, Math.Min(point.Y, zero), Math.Max(point.Y, zero));
            }

            // The glow: wider, fainter strokes under the line itself.
            foreach (var (width, alpha) in new[] { (S(10), 18), (S(6), 36), (S(4), 60) })
            {
                using var glow = new Pen(Color.FromArgb(_active ? alpha : alpha / 2, accent), width) { LineJoin = LineJoin.Round };
                graphics.DrawPath(glow, curve);
            }

            using var line = new Pen(accent, S(2)) { LineJoin = LineJoin.Round };
            graphics.DrawPath(line, curve);
        }

        using var thumb = new SolidBrush(Color.White);
        var valueFont = SemiBoldFont(12);
        var bandFont = SemiBoldFont(13);

        for (var band = 0; band < points.Length; band++)
        {
            var point = points[band];
            var grow = band == _dragging || band == _hovered ? S(1) : 0;
            var box = new RectangleF(point.X - ThumbHalf - grow, point.Y - ThumbHalf - grow, 2 * (ThumbHalf + grow), 2 * (ThumbHalf + grow));

            using (var shape = RoundedButton.RoundedPath(box, S(2f)))
                graphics.FillPath(thumb, shape);

            var gain = _gains[band];
            DrawCentered(graphics, FormatGain(gain), valueFont, gain == 0 ? SubtleTextColor : Color.White, point.X, GridTop - S(22));
            DrawCentered(graphics, EqBands.Label(EqBands.Frequencies[band]), bandFont, SubtleTextColor, point.X, GridBottom + S(14));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _wheelSettle.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>A gain as written above its band: +5.5, 0, -1.</summary>
    internal static string FormatGain(double gain) =>
        (gain > 0 ? "+" : "") + gain.ToString("0.#", CultureInfo.CurrentCulture);

    private void PaintGrid(Graphics graphics)
    {
        using var solid = new Pen(TrackColor, 1);
        using var dashed = new Pen(DividerColor, 1) { DashPattern = [3, 4] };
        var font = SemiBoldFont(11);

        foreach (var gain in new[] { 12, 6, 0, -6, -12 })
        {
            var y = YOf(gain);
            graphics.DrawLine(gain == 0 ? solid : dashed, FirstX - S(22), y, LastX + S(16), y);

            var text = gain > 0 ? $"+{gain}" : gain.ToString(CultureInfo.InvariantCulture);
            var size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(graphics, text, font, new Point(FirstX - S(30) - size.Width, (int)y - size.Height / 2), MutedColor, TextFormatFlags.NoPadding);
        }
    }

    private static void DrawCentered(Graphics graphics, string text, Font font, Color color, float x, float y)
    {
        var size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(graphics, text, font, new Point((int)Math.Round(x - size.Width / 2f), (int)Math.Round(y - size.Height / 2f)), color, TextFormatFlags.NoPadding);
    }

    // A thin upright bar with round ends, between two heights.
    private static void FillBar(Graphics graphics, Brush brush, float x, float top, float bottom)
    {
        if (bottom - top < 1)
            return;

        using var path = RoundedButton.RoundedPath(new RectangleF(x - TrackWidth / 2f, top, TrackWidth, bottom - top), TrackWidth / 2f);
        graphics.FillPath(brush, path);
    }

    // A smooth line through every point (Catmull-Rom, drawn as Bézier pieces).
    private static GraphicsPath CurvePath(PointF[] points)
    {
        var path = new GraphicsPath();

        for (var index = 0; index < points.Length - 1; index++)
        {
            var before = points[Math.Max(0, index - 1)];
            var from = points[index];
            var to = points[index + 1];
            var after = points[Math.Min(points.Length - 1, index + 2)];

            path.AddBezier(
                from,
                new PointF(from.X + (to.X - before.X) / 6, from.Y + (to.Y - before.Y) / 6),
                new PointF(to.X - (after.X - from.X) / 6, to.Y - (after.Y - from.Y) / 6),
                to);
        }

        return path;
    }

    private void SetGain(int band, double gain)
    {
        var snapped = EqBands.Snap(gain);

        if (snapped == _gains[band])
            return;

        _gains[band] = snapped;
        Invalidate();
        GainsChanged?.Invoke(this, EventArgs.Empty);
    }

    private float Spacing => (LastX - FirstX) / (float)(EqBands.Count - 1);

    private float XOf(int band) => FirstX + band * Spacing;

    private float YOf(double gain) =>
        (float)((GridTop + GridBottom) / 2.0 - gain / EqBands.Range * (GridBottom - GridTop) / 2.0);

    private double GainAt(int y) =>
        ((GridTop + GridBottom) / 2.0 - y) / ((GridBottom - GridTop) / 2.0) * EqBands.Range;

    // The band whose column holds x: half the spacing either side of it.
    private int? BandAt(int x)
    {
        var band = (int)Math.Round((x - FirstX) / Spacing);
        return band >= 0 && band < EqBands.Count && Math.Abs(x - XOf(band)) <= Spacing / 2 ? band : null;
    }
}
