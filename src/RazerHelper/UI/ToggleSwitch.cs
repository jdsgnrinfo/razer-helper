using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// An on/off switch: a pill-shaped track, dark grey when off (a little lighter
/// under the pointer) and Razer green when on, with a round white thumb that
/// sits right when on. No outline; a green ring when the keys reach it.
/// It feels like a finger on it: pressing (the mouse or Space) stretches the
/// thumb towards the other side, keeping its own edge where it is; letting go
/// sends it across on a spring that lands without overshooting, while the
/// green fades in or out. A change that came without a press (Enter, or the
/// app setting it) gives the thumb a brief stretch as it travels instead.
/// With Windows' animation effects off it just changes. Everything else
/// (Checked, events, Space to toggle) is the ordinary CheckBox.
/// </summary>
internal sealed class ToggleSwitch : CheckBox
{
    private static int TrackWidth => S(42);
    private static int TrackHeight => S(24);
    private static int Inset => S(3);
    private static int ThumbSize => S(18);

    // How far the thumb widens towards the other side while pressed.
    private static int PressStretch => S(5);

    // The stretch a change without a press gives: up to 16% wider and back.
    private const double PulseSeconds = 0.34;
    private const double PulseWidth = 0.16;
    private const double FadeSeconds = 0.25;
    // A change this soon after a press already had its stretch.
    private const double PressMemory = 0.25;

    private static readonly Color OffTrack = Color.FromArgb(0x3A, 0x3A, 0x3A);
    private static readonly Color OffTrackHover = Color.FromArgb(0x47, 0x47, 0x47);
    private static readonly Color DisabledTrack = Color.FromArgb(0x2A, 0x2A, 0x2A);

    // Lands on its end without overshooting the state it reports.
    private static readonly Spring.Feel Glide = Spring.Feel.Of(0.3, 0);

    // 0 is fully off, 1 fully on, in between while the thumb travels.
    private readonly Spring _travel = new(0, 0.001);
    // Pixels the pressed thumb has widened by.
    private readonly Spring _stretch = new(0, 0.05);

    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;

    private bool _pressed;
    private bool _hovered;
    private double _releasedAt = double.NegativeInfinity;
    private double _pulseAt = double.NegativeInfinity;
    private double _fadeAt = double.NegativeInfinity;
    // The green's strength when the last fade began.
    private float _fadeFrom;
    private float _green;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        AutoSize = false;
        Cursor = Cursors.Hand;
        Size = GetPreferredSize(Size.Empty);

        _frames.Tick += (_, _) => Frame();
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(TrackWidth + S(8) + Padding.Horizontal, TrackHeight + S(6) + Padding.Vertical);

    private double Now => _clock.Elapsed.TotalSeconds;

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);

        var target = Checked ? 1 : 0;

        // Before the window is on screen (the saved setting being loaded),
        // there is nothing to watch: show the state at once.
        if (!IsHandleCreated || !Visible || Motion.Reduced)
        {
            _travel.Jump(target);
            _green = target;
            _fadeAt = double.NegativeInfinity;
            Invalidate();
            return;
        }

        _travel.To(target, Glide);
        _fadeFrom = _green;
        _fadeAt = Now;

        // A press already stretched the thumb; anything else gets the travelling stretch.
        if (Now - _releasedAt > PressMemory)
            _pulseAt = Now;

        StartFrames();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
            Press(true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        // Marked before the CheckBox toggles, so the change knows a press stretched it.
        if (_pressed)
            _releasedAt = Now;

        Press(false);
        base.OnMouseUp(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Press(false);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
            Press(true);

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _pressed)
        {
            _releasedAt = Now;
            Press(false);
        }

        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Press(false);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var background = Parent?.BackColor ?? BackgroundColor;
        graphics.Clear(background);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new RectangleF(
            Padding.Left + (Width - Padding.Horizontal - TrackWidth) / 2f,
            (Height - TrackHeight) / 2f,
            TrackWidth,
            TrackHeight);

        // Only keyboard focus (Tab) shows a ring: green, with a gap, as on the sliders.
        if (Focused && ShowFocusCues)
        {
            using var ring = new SolidBrush(RazerGreen);
            using var gap = new SolidBrush(background);
            using var outer = Pill(RectangleF.Inflate(track, S(3), S(3)));
            using var inner = Pill(RectangleF.Inflate(track, S(1.5f), S(1.5f)));
            graphics.FillPath(ring, outer);
            graphics.FillPath(gap, inner);
        }

        // The off fill, lighter under the pointer, with the green faded in over it.
        var off = !Enabled ? DisabledTrack : _hovered ? OffTrackHover : OffTrack;
        var on = Enabled ? RazerGreen : SystemColors.GrayText;

        using (var path = Pill(track))
        using (var fill = new SolidBrush(Motion.Blend(off, on, Math.Clamp(_green, 0, 1))))
            graphics.FillPath(fill, path);

        // The thumb: pressed, it widens towards the other side and keeps its own
        // edge; on its way it can stretch a little about its middle.
        var travel = TrackWidth - 2 * Inset - ThumbSize;
        var stretch = (float)_stretch.Position;
        var position = (float)Math.Clamp(_travel.Position, 0, 1);
        var width = ThumbSize + stretch;
        var left = track.Left + Inset + position * (travel - stretch);
        var pulse = (float)Pulse();

        if (pulse > 0)
        {
            var grown = width * (1 + pulse);
            left -= (grown - width) / 2;
            width = grown;
        }

        var thumb = new RectangleF(left, track.Top + Inset, width, ThumbSize);

        using (var shadow = new SolidBrush(Color.FromArgb(70, Color.Black)))
        using (var shadowPath = Pill(new RectangleF(thumb.X, thumb.Y + S(1), thumb.Width, thumb.Height)))
            graphics.FillPath(shadow, shadowPath);

        using var thumbPath = Pill(thumb);
        graphics.FillPath(Brushes.White, thumbPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    private void Press(bool pressed)
    {
        if (_pressed == pressed)
            return;

        _pressed = pressed;

        if (!Enabled || Motion.Reduced)
        {
            _stretch.Jump(0);
            Invalidate();
            return;
        }

        _stretch.To(pressed ? PressStretch : 0, Spring.Snappy);
        StartFrames();
    }

    // The travelling stretch: out quickly, then back, over a third of a second.
    private double Pulse()
    {
        var t = (Now - _pulseAt) / PulseSeconds;

        if (t < 0 || t >= 1)
            return 0;

        return t < 0.4
            ? PulseWidth * (1 - Math.Pow(1 - t / 0.4, 2))
            : PulseWidth * (1 - Motion.Ease((float)((t - 0.4) / 0.6)));
    }

    private void StartFrames()
    {
        if (!_frames.Enabled)
        {
            _lastFrame = Now;
            _frames.Start();
        }

        Invalidate();
    }

    private void Frame()
    {
        var now = Now;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        var moving = _travel.Step(seconds) | _stretch.Step(seconds);

        // The green crossfades on its own clock, easing in and out.
        var fade = (now - _fadeAt) / FadeSeconds;
        var target = Checked ? 1f : 0f;
        _green = fade >= 1 ? target : _fadeFrom + (target - _fadeFrom) * Motion.Ease((float)fade);

        Invalidate();

        if (!moving && fade >= 1 && Pulse() == 0 && now - _pulseAt >= PulseSeconds)
            _frames.Stop();
    }

    private static GraphicsPath Pill(RectangleF bounds) => RoundedButton.RoundedPath(bounds, bounds.Height / 2);
}
