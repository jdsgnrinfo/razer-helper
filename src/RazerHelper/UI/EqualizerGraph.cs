using System.ComponentModel;
using System.Diagnostics;
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
/// double click puts it back to 0. With the focus, Left and Right pick a band
/// and Up and Down move it (Shift, Page Up and Page Down by 3 dB; Home and End
/// to the ends). Off, the curve and fills turn grey.
/// The thumbs move on the same springs as the app's other sliders: they follow
/// the pointer and grow while held, spring to a clicked spot, give like rubber
/// past ±12, strain and come back when a key pushes at an end, and a new curve
/// (a preset, another device) sweeps in band by band. A bubble over the moving
/// thumb shows its gain, and the gains above the bands roll as they change.
/// </summary>
/// <remarks>
/// <see cref="GainsChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> once the user lets go, the wheel stops, or a key is released.
/// </remarks>
internal sealed class EqualizerGraph : Control
{
    private static int ThumbRadius => S(8);
    private static int TrackWidth => S(5);
    private static int Corner => S(6);
    private static int Stretch => S(9);
    private static int Push => S(150);
    private static int CarryStep => S(12);

    private const double Lifted = 1.16;
    // A new curve reaches each band this much after the one before.
    private const double SweepSeconds = 0.022;
    private const int BubbleLinger = 700;
    private const double LargeStep = 3;

    // The grid's top and bottom (+12 and -12 dB), and its first and last band.
    private int GridTop => S(38);
    private int GridBottom => Height - S(42);
    private int FirstX => S(58);
    private int LastX => Width - S(28);

    // The sidebar's background, a step lighter than the page.
    private static readonly Color PanelColor = Sidebar.NavColor;
    private static readonly Color MutedColor = OffColor;

    private readonly double[] _gains = EqBands.Flat();

    // Where each thumb is drawn, in dB, and how big it is.
    private readonly Spring[] _shown = [.. Enumerable.Range(0, EqBands.Count).Select(_ => new Spring(0, 0.005))];
    private readonly Spring[] _lifts = [.. Enumerable.Range(0, EqBands.Count).Select(_ => new Spring(1, 0.002))];
    // When a new curve's sweep reaches each band; infinity once it has.
    private readonly double[] _sweepAt = [.. Enumerable.Repeat(double.PositiveInfinity, EqBands.Count)];
    private readonly RollingText[] _labels = [.. Enumerable.Range(0, EqBands.Count).Select(_ => new RollingText())];

    private int _dragging = -1;
    private int _hovered = -1;
    private double _pointer;
    private double _grab;
    private readonly Spring _catchUp = new(0, 0.005);
    private readonly List<(double Time, double Position)> _trail = [];

    private int _focusBand;
    private bool _keyFocus;
    private bool _keyMoved;

    private int _bubbleBand = -1;
    private readonly Spring _bubble = new(0, 0.002);
    private readonly RollingText _bubbleText = new();

    private readonly System.Windows.Forms.Timer _wheelSettle = new() { Interval = 400 };
    private readonly System.Windows.Forms.Timer _bubbleLinger = new() { Interval = BubbleLinger };
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;

    public EqualizerGraph()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = BackgroundColor;
        Cursor = Cursors.Hand;
        TabStop = true;

        for (var band = 0; band < _labels.Length; band++)
            _labels[band].Set(FormatGain(0), animate: false);

        _wheelSettle.Tick += (_, _) =>
        {
            _wheelSettle.Stop();
            Committed?.Invoke(this, EventArgs.Empty);
        };
        _bubbleLinger.Tick += (_, _) =>
        {
            _bubbleLinger.Stop();
            HideBubble();
        };
        _frames.Tick += (_, _) => Frame();
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

    /// <summary>
    /// Shows a curve without raising any event. On screen, the thumbs sweep to
    /// it band by band; otherwise they are simply put there.
    /// </summary>
    public void Show(IReadOnlyList<double> gains)
    {
        var animate = IsHandleCreated && Visible && !Motion.Reduced;
        var now = _clock.Elapsed.TotalSeconds;
        var order = 0;

        for (var band = 0; band < _gains.Length; band++)
        {
            var gain = band < gains.Count ? EqBands.Snap(gains[band]) : 0;

            if (band == _dragging)
                continue;

            _gains[band] = gain;
            _labels[band].Set(FormatGain(gain), animate);

            if (!animate)
            {
                _shown[band].Jump(gain);
                _sweepAt[band] = double.PositiveInfinity;
            }
            else if (_shown[band].Target != gain)
            {
                _sweepAt[band] = now + order++ * SweepSeconds;
            }
        }

        // A bubble left over from the last move would show an old gain.
        if (_dragging < 0)
        {
            _bubbleLinger.Stop();
            _bubble.Jump(0);
            _bubbleBand = -1;
        }

        if (animate)
            StartFrames();

        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown ||
        base.IsInputKey(keyData);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || BandAt(e.X) is not { } band)
            return;

        Focus();
        _keyFocus = false;
        _focusBand = band;
        _dragging = band;
        _sweepAt[band] = double.PositiveInfinity;
        _trail.Clear();
        _trail.Add((_clock.Elapsed.TotalMilliseconds, GainAt(e.Y)));

        // Grabbing the thumb keeps it under the pointer as it was; a click
        // elsewhere in the column springs it over from where it was.
        var current = _shown[band].Position;

        if (Math.Abs(e.Y - YOf(Rubberized(current))) <= ThumbRadius + S(2))
        {
            _grab = current - GainAt(e.Y);
            _catchUp.Jump(0);
        }
        else
        {
            _grab = 0;
            _catchUp.Jump(current - GainAt(e.Y));
            Animate(_catchUp, 0, Spring.Snappy);
        }

        _pointer = GainAt(e.Y) + _grab;
        Animate(_lifts[band], Lifted, Spring.Snappy);
        FollowPointer();
        StartFrames();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging >= 0)
        {
            var now = _clock.Elapsed.TotalMilliseconds;
            _trail.Add((now, GainAt(e.Y)));
            _trail.RemoveAll(point => now - point.Time > 80 && _trail.Count > 2);
            _pointer = GainAt(e.Y) + _grab;
            FollowPointer();
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

        var band = _dragging;
        var from = Dragged;
        var velocity = SliderPhysics.ReleaseVelocity(_trail, _clock.Elapsed.TotalMilliseconds) + _catchUp.Velocity;
        var stepHeight = EqBands.Step * PixelsPerDecibel;

        // On a coarse grid a quick release carries the thumb one step further, never more.
        var carry = stepHeight >= CarryStep ? Math.Clamp(SliderPhysics.Project(velocity), -EqBands.Step, EqBands.Step) : 0;

        _dragging = -1;
        _catchUp.Jump(0);
        _shown[band].Jump(from);
        SetGain(band, Math.Clamp(from, -EqBands.Range, EqBands.Range) + carry);
        Animate(_shown[band], _gains[band], Spring.Snappy, velocity);
        Animate(_lifts[band], 1, Spring.Snappy);
        LetBubbleGo();
        StartFrames();
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (BandAt(e.X) is { } band)
        {
            SetGain(band, 0);
            Animate(_shown[band], 0, Spring.Snappy);
            StartFrames();
            Committed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (BandAt(e.X) is not { } band)
            return;

        MoveBand(band, _gains[band] + Math.Sign(e.Delta) * EqBands.Step, Math.Sign(e.Delta));
        _wheelSettle.Stop();
        _wheelSettle.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var band = _focusBand;
        var step = e.Shift ? LargeStep : EqBands.Step;

        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Right:
                _focusBand = Math.Clamp(band + (e.KeyCode == Keys.Right ? 1 : -1), 0, EqBands.Count - 1);
                _keyFocus = true;
                Invalidate();
                e.Handled = true;
                return;
            case Keys.Up:
                _keyMoved |= MoveBand(band, _gains[band] + step, 1);
                break;
            case Keys.Down:
                _keyMoved |= MoveBand(band, _gains[band] - step, -1);
                break;
            case Keys.PageUp:
                _keyMoved |= MoveBand(band, _gains[band] + LargeStep, 1);
                break;
            case Keys.PageDown:
                _keyMoved |= MoveBand(band, _gains[band] - LargeStep, -1);
                break;
            case Keys.Home:
                _keyMoved |= MoveBand(band, -EqBands.Range, 0);
                break;
            case Keys.End:
                _keyMoved |= MoveBand(band, EqBands.Range, 0);
                break;
            default:
                return;
        }

        _keyFocus = true;
        e.Handled = true;
        LetBubbleGo(BubbleLinger * 2);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (!_keyMoved)
            return;

        _keyMoved = false;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _keyFocus = false;
        Invalidate();
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

        var points = Enumerable.Range(0, EqBands.Count).Select(band => new PointF(XOf(band), YOf(Shown(band)))).ToArray();
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

        var valueFont = SemiBoldFont(12);
        var bandFont = SemiBoldFont(13);
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        for (var band = 0; band < points.Length; band++)
        {
            var point = points[band];
            var radius = ThumbRadius * (float)_lifts[band].Position;
            ThemedSlider.PaintThumb(graphics, point, radius, band == _dragging || band == _hovered, _keyFocus && Focused && band == _focusBand, PanelColor);

            // The gain above the band, rolling as it changes.
            var color = _gains[band] == 0 ? SubtleTextColor : Color.White;
            var labelHeight = TextRenderer.MeasureText(graphics, "0", valueFont, Size.Empty, Flags).Height;
            _labels[band].Paint(
                graphics,
                new RectangleF(point.X - Spacing / 2, GridTop - S(22) - labelHeight / 2f - S(2), Spacing, labelHeight + S(4)),
                HorizontalAlignment.Center,
                text => TextRenderer.MeasureText(graphics, text, valueFont, Size.Empty, Flags),
                (text, at, opacity) => TextRenderer.DrawText(graphics, text, valueFont, Point.Round(at), Motion.Blend(PanelColor, color, Math.Clamp(opacity, 0, 1)), Flags));

            DrawCentered(graphics, EqBands.Label(EqBands.Frequencies[band]), bandFont, SubtleTextColor, point.X, GridBottom + S(14));
        }

        PaintBubble(graphics, points);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wheelSettle.Dispose();
            _bubbleLinger.Dispose();
            _frames.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>A gain as written above its band: +5.5, 0, -1.</summary>
    internal static string FormatGain(double gain) =>
        (gain > 0 ? "+" : "") + gain.ToString("0.#", CultureInfo.CurrentCulture);

    // The bubble over the moving band's thumb, kept inside the panel.
    private void PaintBubble(Graphics graphics, PointF[] points)
    {
        if (_bubbleBand < 0 || _bubble.Position <= 0)
            return;

        var point = points[_bubbleBand];
        var radius = ThumbRadius * (float)_lifts[_bubbleBand].Position;
        var lowest = BubbleArt.PillHeight + BubbleArt.PointerSize + S(2);
        var tip = new PointF(point.X, Math.Max(lowest, point.Y - radius - S(4)));

        var width = BubbleArt.Width(graphics, _bubbleText);
        var left = tip.X - width / 2;
        var shift = Math.Clamp(left, S(4), Width - S(4) - width) - left;

        BubbleArt.Paint(graphics, tip, shift, _bubbleText, (float)_bubble.Position);
    }

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

    // Moves a band by a key or the wheel, springing its thumb there; at an end
    // it strains that way and comes back. True if the gain changed.
    private bool MoveBand(int band, double gain, int push)
    {
        var before = _gains[band];
        _sweepAt[band] = double.PositiveInfinity;

        if (EqBands.Snap(gain) == before && push != 0)
            Animate(_shown[band], before, Spring.Morph, push * Push / PixelsPerDecibel);
        else
        {
            SetGain(band, gain);
            Animate(_shown[band], _gains[band], Spring.Snappy);
        }

        ShowBubble(band);
        LetBubbleGo(BubbleLinger * 2);
        StartFrames();
        return _gains[band] != before;
    }

    private void SetGain(int band, double gain)
    {
        var snapped = EqBands.Snap(gain);

        if (snapped == _gains[band])
            return;

        _gains[band] = snapped;
        _labels[band].Set(FormatGain(snapped));
        Invalidate();
        GainsChanged?.Invoke(this, EventArgs.Empty);
    }

    // The dragged band follows the pointer; its gain takes the nearest step.
    private void FollowPointer()
    {
        SetGain(_dragging, Math.Clamp(Dragged, -EqBands.Range, EqBands.Range));
        ShowBubble(_dragging);
        Invalidate();
    }

    private double Dragged => _pointer + _catchUp.Position;

    // Where a band's thumb is drawn, in dB, with the stretch past the ends.
    private double Shown(int band) => Rubberized(band == _dragging ? Dragged : _shown[band].Position);

    private double Rubberized(double gain)
    {
        var range = EqBands.Range;

        if (Math.Abs(gain) <= range)
            return gain;

        var past = (Math.Abs(gain) - range) * PixelsPerDecibel;
        return Math.Sign(gain) * (range + SliderPhysics.Rubber(past, Stretch) / PixelsPerDecibel);
    }

    private void ShowBubble(int band)
    {
        _bubbleLinger.Stop();
        var text = FormatGain(_gains[band]) + " dB";

        if (_bubbleBand != band || _bubble.Target <= 0)
        {
            _bubbleText.Set(text, animate: false);

            if (_bubbleBand != band)
                _bubble.Jump(0);
        }
        else
        {
            _bubbleText.Set(text);
        }

        _bubbleBand = band;
        Animate(_bubble, 1, Spring.Snappy);
        StartFrames();
    }

    private void HideBubble()
    {
        if (_dragging >= 0)
            return;

        Animate(_bubble, 0, Spring.Snappy);
        StartFrames();
    }

    private void LetBubbleGo(int milliseconds = BubbleLinger)
    {
        _bubbleLinger.Stop();
        _bubbleLinger.Interval = milliseconds;
        _bubbleLinger.Start();
    }

    // A spring heads for its target, or with animation effects off jumps there.
    private static void Animate(Spring spring, double target, Spring.Feel feel, double? velocity = null)
    {
        if (Motion.Reduced)
            spring.Jump(target);
        else
            spring.To(target, feel, velocity);
    }

    private void StartFrames()
    {
        if (!_frames.Enabled)
        {
            _lastFrame = _clock.Elapsed.TotalSeconds;
            _frames.Start();
        }

        Invalidate();
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;
        var busy = _dragging >= 0;

        for (var band = 0; band < EqBands.Count; band++)
        {
            // A new curve's sweep starts each band in turn.
            if (now >= _sweepAt[band])
            {
                _sweepAt[band] = double.PositiveInfinity;
                _shown[band].To(_gains[band], Spring.Snappy);
            }

            busy |= _shown[band].Step(seconds) | _lifts[band].Step(seconds) | !double.IsPositiveInfinity(_sweepAt[band]) | _labels[band].Rolling;
        }

        busy |= _catchUp.Step(seconds) | _bubble.Step(seconds) | _bubbleText.Rolling;

        if (_dragging >= 0)
            FollowPointer();

        if (_bubble.Target <= 0 && !_bubble.Moving)
            _bubbleBand = -1;

        Invalidate();

        if (!busy)
            _frames.Stop();
    }

    private double PixelsPerDecibel => (GridBottom - GridTop) / (2.0 * EqBands.Range);

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
