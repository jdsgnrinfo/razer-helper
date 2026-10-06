using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark, flat slider that snaps to fixed steps: a round-ended track filling
/// in green, with a dot at each step when there are few, and a round white
/// thumb. The stock TrackBar ignores the theme, so this draws it all itself.
/// It moves on springs: the thumb follows the pointer exactly and grows a
/// little while held; a click on the track sends it over from where it was;
/// a release with speed carries it at most a step further before it settles;
/// past either end it gives like rubber; a key at an end makes it push and
/// spring back. A bubble over the thumb shows the value while it moves.
/// With Windows' animation effects off it moves without any of that.
/// </summary>
/// <remarks>
/// <see cref="ValueChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> fires once when the user lets go (mouse up, or a
/// key that moved the value), which is the moment to act on the value.
/// </remarks>
internal sealed class ThemedSlider : Control
{
    private static int ThumbRadius => S(9);
    private static int TrackHeight => S(6);

    // The track starts and ends this far in, leaving room for the grown thumb and its stretch.
    private static int Inset => S(16);

    // How far past an end the thumb can be pulled, and a key's push at an end.
    private static int Stretch => S(5);
    private static int Push => S(150);

    // A release can carry the thumb one step further only when steps are at least this far apart.
    private static int CarryStep => S(12);

    // Steps shown as dots when there are no more than this many.
    private const int MostDots = 20;

    private const double Lifted = 1.16;
    private const int BubbleLinger = 700;

    // Unavailable: the filled part is a mid grey instead of green.
    private static readonly Color DisabledFillColor = Color.FromArgb(0x78, 0x78, 0x78);

    // Drawing objects shared by every slider and reused on every repaint.
    private static readonly SolidBrush TrackBrush = new(TrackColor);
    private static readonly SolidBrush FillBrush = new(RazerGreen);
    private static readonly SolidBrush DisabledFillBrush = new(DisabledFillColor);
    private static readonly SolidBrush DotBrush = new(Color.FromArgb(0x4A, 0x4A, 0x4A));
    private static readonly SolidBrush FilledDotBrush = new(Color.FromArgb(0x1B, 0x5C, 0x12));

    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _step;

    private int _value;
    private bool _dragging;
    private bool _hovered;
    // The ring shows after a key, not after a click.
    private bool _keyFocus;
    private bool _keyMovedValue;

    // Where the thumb is, in pixels: the pointer plus a catch-up offset while
    // dragging, a spring towards the value's place otherwise.
    private readonly Spring _position = new(0, 0.1);
    private readonly Spring _catchUp = new(0, 0.1);
    private readonly Spring _lift = new(1, 0.002);
    private double _pointer;
    private double _grab;
    private readonly List<(double Time, double Position)> _trail = [];

    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _bubbleLinger = new() { Interval = BubbleLinger };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private bool _placed;

    public ThemedSlider(int minimum, int maximum, int step)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimum, maximum);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, 0);

        _minimum = minimum;
        _maximum = maximum;
        _step = step;
        _value = minimum;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick,
            true);
        TabStop = true;
        BackColor = CardColor;
        Height = S(24);
        Cursor = Cursors.Hand;
        _frames.Tick += (_, _) => Frame();
        _bubbleLinger.Tick += (_, _) =>
        {
            _bubbleLinger.Stop();

            if (!_dragging)
                SliderBubble.Shared.Release(this);
        };
    }

    /// <summary>True while the user is moving it, with the mouse or a held key.</summary>
    public bool IsBeingMoved => _dragging || _keyMovedValue;

    public event EventHandler? ValueChanged;

    public event EventHandler? Committed;

    /// <summary>How the bubble writes the value: "80 %", "4". The number alone if not set.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, string>? Format { get; set; }

    /// <summary>
    /// False draws the slider like a disabled one and ignores the mouse and
    /// keyboard, yet leaves it enabled underneath, because WinForms shows no
    /// tooltip on a disabled control. The same approach as the unavailable
    /// buttons, so hovering can say why it cannot be used.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Available
    {
        get => _available;
        set
        {
            if (_available == value)
                return;

            _available = value;
            TabStop = value;
            _dragging = false;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            SettleThumb();
            SliderBubble.Shared.Release(this);
            Invalidate();
        }
    }

    private bool _available = true;

    // Enabled and available: the only state in which it reacts or looks live.
    private bool IsLive => Enabled && _available;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            // Set from outside (a refresh, say), the thumb goes straight there.
            if (ChangeValue(value) && !_dragging && !_position.Moving)
                SettleThumb();
        }
    }

    // Takes the step nearest <paramref name="value"/>, leaving the thumb where it is; true if it changed.
    private bool ChangeValue(int value)
    {
        var snapped = Snap(value);

        if (snapped == _value)
            return false;

        _value = snapped;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Sets the value from outside and lets the thumb glide there, unless the user is moving it.</summary>
    public void GlideTo(int value)
    {
        if (IsBeingMoved)
            return;

        // Off screen there is nothing to watch: it goes straight there.
        if (!IsHandleCreated || !Visible)
        {
            Value = value;
            return;
        }

        ChangeValue(value);
        SpringTo(XAt(_value));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown ||
        base.IsInputKey(keyData);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || !IsLive)
            return;

        // Where the thumb is, read before the drag starts (from then on it follows the pointer).
        var current = ThumbPosition;

        Focus();
        _keyFocus = false;
        _dragging = true;
        _trail.Clear();
        _trail.Add((_clock.Elapsed.TotalMilliseconds, e.X));

        // Grabbing the thumb keeps it where it is under the pointer; a click on
        // the track springs it over from where it was.

        if (Math.Abs(e.X - ShownX(current)) <= ThumbRadius + S(2))
        {
            _grab = current - e.X;
            _catchUp.Jump(0);
        }
        else
        {
            _grab = 0;
            _catchUp.Jump(current - e.X);
            Animate(_catchUp, 0, Spring.Snappy);
        }

        _position.Jump(current);
        _pointer = e.X + _grab;
        Animate(_lift, Lifted, Spring.Snappy);
        FollowPointer();
        StartFrames();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
        {
            var hovered = IsLive && Math.Abs(e.X - ShownX(ThumbPosition)) <= ThumbRadius + S(4);

            if (hovered != _hovered)
            {
                _hovered = hovered;
                Invalidate();
            }

            return;
        }

        var now = _clock.Elapsed.TotalMilliseconds;
        _trail.Add((now, e.X));
        _trail.RemoveAll(point => now - point.Time > 80 && _trail.Count > 2);
        _pointer = e.X + _grab;
        FollowPointer();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hovered)
        {
            _hovered = false;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        var from = ThumbPosition;
        var velocity = SliderPhysics.ReleaseVelocity(_trail, _clock.Elapsed.TotalMilliseconds) + _catchUp.Velocity;
        var stepWidth = TrackWidth * _step / (double)(_maximum - _minimum);

        // On a coarse grid a quick release carries the thumb one step further, never more.
        var carry = stepWidth >= CarryStep ? Math.Clamp(SliderPhysics.Project(velocity), -stepWidth, stepWidth) : 0;

        _dragging = false;
        _catchUp.Jump(0);
        _position.Jump(from);
        ChangeValue(ValueAt(Math.Clamp(from, XAt(_minimum), XAt(_maximum)) + carry));
        Animate(_position, XAt(_value), Spring.Snappy, velocity);
        Animate(_lift, 1, Spring.Snappy);
        StartFrames();
        LetBubbleGo();
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsLive)
            return;

        var largeStep = Math.Max(_step, Snap(_minimum + (_maximum - _minimum) / 10) - _minimum);
        var step = e.Shift ? largeStep : _step;

        var (target, push) = e.KeyCode switch
        {
            Keys.Left or Keys.Down => (_value - step, -1),
            Keys.Right or Keys.Up => (_value + step, 1),
            Keys.PageDown => (_value - largeStep, -1),
            Keys.PageUp => (_value + largeStep, 1),
            Keys.Home => (_minimum, 0),
            Keys.End => (_maximum, 0),
            _ => ((int?)null, 0)
        };

        if (target is null)
            return;

        e.Handled = true;
        _keyFocus = true;

        if (Snap(target.Value) == _value && push != 0)
        {
            // At an end the thumb strains towards the key and springs home, so the key still answers.
            Animate(_position, XAt(_value), Spring.Morph, push * Push);
        }
        else
        {
            var before = _value;
            _position.Jump(ThumbPosition);
            ChangeValue(target.Value);
            Animate(_position, XAt(_value), Spring.Snappy);
            _keyMovedValue |= _value != before;
        }

        StartFrames();
        ShowBubble();
        LetBubbleGo(BubbleLinger * 2);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Only a key that actually moved the slider is worth acting on;
        // Tab and friends are not.
        if (!_keyMovedValue)
            return;

        _keyMovedValue = false;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _keyFocus = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible)
            SliderBubble.Shared.Release(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var thumbX = (float)ShownX(ThumbPosition);
        var start = XAt(_minimum);
        var end = XAt(_maximum);

        // Track, round-ended, then the filled part up to the thumb.
        var top = TrackY - TrackHeight / 2f;
        FillPill(graphics, TrackBrush, new RectangleF(start - TrackHeight / 2f, top, end - start + TrackHeight, TrackHeight));
        FillPill(graphics, IsLive ? FillBrush : DisabledFillBrush, new RectangleF(start - TrackHeight / 2f, top, thumbX - start + TrackHeight / 2f, TrackHeight));

        // A dot at each step, when they are few enough to tell apart.
        if ((_maximum - _minimum) / _step <= MostDots)
        {
            var dot = S(4);

            for (var value = _minimum; value <= _maximum; value += _step)
            {
                var x = XAt(value);
                graphics.FillEllipse(x < thumbX - dot ? (IsLive ? FilledDotBrush : DotBrush) : DotBrush, x - dot / 2f, TrackY - dot / 2f, dot, dot);
            }
        }

        PaintThumb(graphics, new PointF(thumbX, TrackY), ThumbRadius * (float)_lift.Position, IsLive && (_hovered || _dragging), _keyFocus && Focused, BackColor);
    }

    /// <summary>
    /// A slider's thumb: a white disc with a soft shadow, a faint green halo
    /// while the pointer is on it, and a green ring when the keys are moving it.
    /// </summary>
    internal static void PaintThumb(Graphics graphics, PointF center, float radius, bool halo, bool ring, Color background)
    {
        if (ring)
        {
            using var green = new SolidBrush(RazerGreen);
            using var gap = new SolidBrush(background);
            graphics.FillEllipse(green, center.X - radius - S(4), center.Y - radius - S(4), 2 * (radius + S(4)), 2 * (radius + S(4)));
            graphics.FillEllipse(gap, center.X - radius - S(2), center.Y - radius - S(2), 2 * (radius + S(2)), 2 * (radius + S(2)));
        }
        else if (halo)
        {
            using var soft = new SolidBrush(Color.FromArgb(46, RazerGreen));
            graphics.FillEllipse(soft, center.X - radius - S(3), center.Y - radius - S(3), 2 * (radius + S(3)), 2 * (radius + S(3)));
        }

        using (var shadow = new SolidBrush(Color.FromArgb(90, Color.Black)))
            graphics.FillEllipse(shadow, center.X - radius, center.Y - radius + S(1), 2 * radius, 2 * radius);

        graphics.FillEllipse(Brushes.White, center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SettleThumb();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frames.Dispose();
            _bubbleLinger.Dispose();
        }

        base.Dispose(disposing);
    }

    // Where the thumb is now, before any stretch past the ends.
    private double ThumbPosition
    {
        get
        {
            if (_dragging)
                return _pointer + _catchUp.Position;

            if (!_placed)
            {
                _position.Jump(XAt(_value));
                _placed = true;
            }

            return _position.Position;
        }
    }

    // Past an end, the thumb gives less and less.
    private double ShownX(double x)
    {
        var start = XAt(_minimum);
        var end = XAt(_maximum);

        return x < start ? start + SliderPhysics.Rubber(x - start, Stretch)
            : x > end ? end + SliderPhysics.Rubber(x - end, Stretch)
            : x;
    }

    // While dragging, the value takes the step nearest the thumb.
    private void FollowPointer()
    {
        ChangeValue(ValueAt(Math.Clamp(ThumbPosition, XAt(_minimum), XAt(_maximum))));
        ShowBubble();
        Invalidate();
    }

    private void SettleThumb()
    {
        _position.Jump(XAt(_value));
        _catchUp.Jump(0);
        _placed = true;
        Invalidate();
    }

    private void SpringTo(double x)
    {
        _position.Jump(ThumbPosition);
        Animate(_position, x, Spring.Snappy);
        StartFrames();
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
        _lastFrame = _clock.Elapsed.TotalSeconds;

        if (!_frames.Enabled)
            _frames.Start();

        Invalidate();
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        var moving = _position.Step(seconds) | _catchUp.Step(seconds) | _lift.Step(seconds);

        if (_dragging)
            FollowPointer();
        else if (BubbleShowing)
            ShowBubble();

        Invalidate();

        if (!moving && !_dragging)
            _frames.Stop();
    }

    private bool BubbleShowing => _dragging || _bubbleLinger.Enabled;

    private void ShowBubble()
    {
        if (!IsHandleCreated || !Visible)
            return;

        var tip = PointToScreen(new Point((int)Math.Round(ShownX(ThumbPosition)), TrackY - (int)Math.Ceiling(ThumbRadius * _lift.Position) - S(4)));
        var left = PointToScreen(new Point(XAt(_minimum) - Inset, 0)).X;
        var right = PointToScreen(new Point(XAt(_maximum) + Inset, 0)).X;
        var text = Format?.Invoke(_value) ?? _value.ToString(CultureInfo.CurrentCulture);
        SliderBubble.Shared.Follow(this, tip, left, right, text);
    }

    // The bubble stays a moment after the thumb stops, then goes.
    private void LetBubbleGo(int milliseconds = BubbleLinger)
    {
        _bubbleLinger.Stop();
        _bubbleLinger.Interval = milliseconds;
        _bubbleLinger.Start();
    }

    // A bar with fully rounded ends.
    private static void FillPill(Graphics graphics, Brush brush, RectangleF bounds)
    {
        if (bounds.Width <= 0)
            return;

        using var path = RoundedButton.RoundedPath(bounds, bounds.Height / 2);
        graphics.FillPath(brush, path);
    }

    private int TrackY => Height / 2;

    private int TrackWidth => Math.Max(1, Width - Inset * 2 - 1);

    private int XAt(int value) =>
        Inset + (int)Math.Round((value - _minimum) / (double)(_maximum - _minimum) * TrackWidth);

    private int ValueAt(double x)
    {
        var fraction = Math.Clamp((x - Inset) / TrackWidth, 0, 1);
        return Snap(_minimum + (int)Math.Round(fraction * (_maximum - _minimum)));
    }

    private int Snap(int value)
    {
        var clamped = Math.Clamp(value, _minimum, _maximum);
        return _minimum + (int)Math.Round((clamped - _minimum) / (double)_step, MidpointRounding.AwayFromZero) * _step;
    }
}
