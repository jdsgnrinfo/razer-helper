using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>What a loader shows: still working, done, or failed.</summary>
internal enum LoaderStatus
{
    Loading,
    Success,
    Error
}

/// <summary>
/// The app's loader: a green ring of four arcs that turns while the arcs
/// breathe, for while something is being read or done. When it is told how
/// the job went, the turning stops a little further on, the arcs close one
/// after another into a whole circle (green, or red if the job failed), and
/// the circle pops once. Back to loading, the arcs part and turn again.
/// Each stroke is a middle point, a length, a direction, a bend and a width,
/// each on its own spring, so any shape becomes any other and a change
/// halfway keeps its speed. With Windows' animation effects off it shows a
/// still ring or the whole circle, and swaps at once. It only animates while shown.
/// </summary>
internal sealed class LoadingSpinner : Control
{
    // Base-design pixels.
    private const int DefaultDiameter = 36;

    // The drawing's grid: 24 units across, as the loader is designed.
    private const float Grid = 24f;
    private const float StrokeUnits = 2.5f;
    private const float RingRadius = 9f;

    // One turn every 1.1 seconds; the arcs breathe twice as fast.
    private const double TurnsPerSecond = 0.9;
    private const double BreathsPerSecond = 1.6;

    // When done, the arcs close one after another, this far apart.
    private const double StrokeStagger = 0.09;

    private static readonly Color ErrorColor = Color.FromArgb(0xF0, 0x4A, 0x4A);

    private readonly Stroke[] _strokes = [.. Enumerable.Range(0, 4).Select(_ => new Stroke())];
    private readonly Spring _pop = new(1, 0.0005);
    private readonly Spring _tone = new(0, 0.002);
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private double _lastFrame;
    private double _statusAt;
    private bool _placed;

    /// <summary>The circle's size in base-design pixels: 36 unless set smaller, as in a button's place.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Diameter { get; init; } = DefaultDiameter;

    public LoadingSpinner()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        _frames.Tick += (_, _) => Frame();
    }

    /// <summary>Loading, or how the job went. Setting it morphs the drawing.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public LoaderStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            _statusAt = Now;
            AccessibleName = value switch
            {
                LoaderStatus.Success => Core.Localization.L.T("Done"),
                LoaderStatus.Error => Core.Localization.L.T("Failed"),
                _ => Core.Localization.L.T("Loading")
            };

            if (value != LoaderStatus.Loading)
            {
                // The closed circle pops once.
                _pop.Jump(1);
                Animate(_pop, 1, Spring.Feel.Of(0.4, 0.5), velocity: 1.6);
            }

            Animate(_tone, value == LoaderStatus.Error ? 1 : 0, Spring.Feel.Of(0.12, 0));

            if (Motion.Reduced || !Visible)
                PlaceAtRest();

            StartFrames();
        }
    }

    private LoaderStatus _status;

    private double Now => _clock.Elapsed.TotalSeconds;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            if (!_placed)
                PlaceAtRest();

            StartFrames();
        }
        else
        {
            _frames.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (!_placed)
            PlaceAtRest();

        // The 24-unit drawing, centred and scaled to the diameter, popping about its middle.
        var size = S(Diameter);
        var scale = size / Grid * (float)_pop.Position;
        graphics.TranslateTransform(Width / 2f, Height / 2f);
        graphics.ScaleTransform(scale, scale);

        var color = Motion.Blend(RazerGreen, ErrorColor, (float)Math.Clamp(_tone.Position, 0, 1));

        foreach (var stroke in _strokes)
            stroke.Paint(graphics, color);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    private void StartFrames()
    {
        if (!Visible || Motion.Reduced && _placed)
        {
            Invalidate();
            return;
        }

        if (!_frames.Enabled)
        {
            _lastFrame = Now;
            _frames.Start();
        }
    }

    private void Frame()
    {
        var now = Now;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        var poses = Poses(now);
        var moving = false;

        for (var index = 0; index < _strokes.Length; index++)
        {
            // The arcs close one by one; they part all at once.
            var startsAt = _status == LoaderStatus.Loading ? 0 : index * StrokeStagger;

            if (now - _statusAt >= startsAt)
                _strokes[index].Follow(poses[index]);

            moving |= _strokes[index].Step(seconds);
        }

        moving |= _pop.Step(seconds) | _tone.Step(seconds);
        Invalidate();

        // Loading keeps turning; a closed circle stops once it has settled.
        if (_status != LoaderStatus.Loading && !moving)
            _frames.Stop();
    }

    // Puts every stroke where the current state has it, without moving there.
    private void PlaceAtRest()
    {
        var poses = Poses(Motion.Reduced ? 0 : Now);

        for (var index = 0; index < _strokes.Length; index++)
            _strokes[index].Jump(poses[index]);

        _pop.Jump(1);
        _tone.Jump(_status == LoaderStatus.Error ? 1 : 0);
        _placed = true;
        Invalidate();
    }

    // Where each of the four strokes belongs at this moment.
    private Pose[] Poses(double time) => _status == LoaderStatus.Loading
        ? Ring(Turn(time), Breath(time))
        // Done: the turning stops a little further on, upright, and the arcs
        // close into one whole circle (red if the job failed).
        : Ring(Upright(Turn(_statusAt)), Closed);

    // How far the ring has turned by <paramref name="time"/>, in radians.
    private static double Turn(double time) => Motion.Reduced ? 0 : time * TurnsPerSecond * 2 * Math.PI;

    // The next quarter turn ahead, where the ring settles.
    private static double Upright(double turn) => Math.Ceiling(turn / (Math.PI / 2)) * (Math.PI / 2);

    // Each arc spans from about a third to two thirds of its quarter as it breathes.
    private static double Breath(double time)
    {
        var breath = Motion.Reduced ? 0.5 : (Math.Sin(time * BreathsPerSecond * 2 * Math.PI) + 1) / 2;
        return Math.PI / 2 * (0.35 + 0.35 * breath);
    }

    // Arcs that meet: a whole circle.
    private const double Closed = Math.PI / 2;

    // The ring: four arcs a quarter apart, turned by <paramref name="turn"/>, each
    // <paramref name="sweep"/> long (radians of the circle).
    private static Pose[] Ring(double turn, double sweep)
    {
        var poses = new Pose[4];

        for (var index = 0; index < 4; index++)
        {
            var angle = turn + index * Math.PI / 2;
            var middle = new PointF((float)(RingRadius * Math.Cos(angle)), (float)(RingRadius * Math.Sin(angle)));

            // The tangent there, going round clockwise on screen; the bend keeps it on the circle.
            poses[index] = new Pose(middle.X, middle.Y, (float)(RingRadius * sweep), (float)(angle + Math.PI / 2), 1 / RingRadius, StrokeUnits);
        }

        return poses;
    }

    private static void Animate(Spring spring, double target, Spring.Feel feel, double? velocity = null)
    {
        if (Motion.Reduced)
            spring.Jump(target);
        else
            spring.To(target, feel, velocity);
    }

    /// <summary>A stroke's shape: its middle, length, direction (radians), bend (1 / radius) and width, in grid units.</summary>
    private readonly record struct Pose(float X, float Y, float Length, float Direction, float Bend, float Width);

    // One of the four strokes, each part on its own spring.
    private sealed class Stroke
    {
        private readonly Spring _x = new(0, 0.002);
        private readonly Spring _y = new(0, 0.002);
        private readonly Spring _length = new(0, 0.002);
        private readonly Spring _direction = new(0, 0.0005);
        private readonly Spring _bend = new(0, 0.0005);
        private readonly Spring _width = new(0, 0.002);

        public void Jump(Pose pose)
        {
            _x.Jump(pose.X);
            _y.Jump(pose.Y);
            _length.Jump(pose.Length);
            _direction.Jump(pose.Direction);
            _bend.Jump(pose.Bend);
            _width.Jump(pose.Width);
        }

        public void Follow(Pose pose)
        {
            if (Motion.Reduced)
            {
                Jump(pose);
                return;
            }

            var feel = Spring.Snappy;
            _x.To(pose.X, feel);
            _y.To(pose.Y, feel);
            _length.To(pose.Length, feel);
            _width.To(pose.Width, feel);
            _bend.To(pose.Bend, feel);

            // A straight stroke reads the same either way round, so it turns the shorter way;
            // a turning ring keeps counting up, so the angle is taken nearest to where it is.
            var period = pose.Bend == 0 ? Math.PI : 2 * Math.PI;
            var direction = pose.Direction + Math.Round((_direction.Position - pose.Direction) / period) * period;
            _direction.To(direction, feel);
        }

        public bool Step(double seconds) =>
            _x.Step(seconds) | _y.Step(seconds) | _length.Step(seconds) | _direction.Step(seconds) | _bend.Step(seconds) | _width.Step(seconds);

        public void Paint(Graphics graphics, Color color)
        {
            var length = (float)_length.Position;
            var width = (float)_width.Position;

            if (length <= 0.05f || width <= 0.05f)
                return;

            // Walks the stroke from its middle both ways: a circular arc of the given
            // bend, or a straight line when there is none.
            const int Steps = 14;
            var points = new PointF[Steps + 1];
            var direction = _direction.Position;
            var bend = _bend.Position;

            for (var step = 0; step <= Steps; step++)
            {
                var s = (step / (double)Steps - 0.5) * length;
                double dx, dy;

                if (Math.Abs(bend) < 1e-4)
                {
                    dx = s * Math.Cos(direction);
                    dy = s * Math.Sin(direction);
                }
                else
                {
                    dx = (Math.Sin(direction + bend * s) - Math.Sin(direction)) / bend;
                    dy = (Math.Cos(direction) - Math.Cos(direction + bend * s)) / bend;
                }

                points[step] = new PointF((float)(_x.Position + dx), (float)(_y.Position + dy));
            }

            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.DrawLines(pen, points);
        }
    }
}
