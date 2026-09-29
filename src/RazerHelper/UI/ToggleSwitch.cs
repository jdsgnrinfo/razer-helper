using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// An on/off switch: a softly rounded track with a black square knob that
/// sits right when on. On fades from dark green to Razer green; off is flat
/// grey. No outline. A change slides the knob across and fades the
/// colors rather than jumping. Everything else (Checked, events, Space to
/// toggle) is the ordinary CheckBox.
/// </summary>
internal sealed class ToggleSwitch : CheckBox
{
    private const double AnimationMilliseconds = 160;

    private static int TrackWidth => S(48);
    private static int TrackHeight => S(24);
    private static int KnobInset => S(3);

    private static readonly Color OffTrackColor = Color.FromArgb(0x68, 0x68, 0x68);
    private static readonly Color SwitchGradientStart = Color.FromArgb(0x14, 0x32, 0x0E);

    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();

    // 0 is fully off, 1 fully on; in between while the knob slides.
    private float _position;
    private float _from;

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

        _animation.Tick += (_, _) => Step();
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new(TrackWidth + S(4) + Padding.Horizontal, TrackHeight + S(4) + Padding.Vertical);

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);

        // Before the window is on screen (the saved setting being loaded),
        // there is nothing to watch: show the state at once.
        if (!IsHandleCreated || !Visible)
        {
            _animation.Stop();
            _position = Checked ? 1 : 0;
            Invalidate();
            return;
        }

        _from = _position;
        _clock.Restart();
        _animation.Start();
    }

    private void Step()
    {
        var target = Checked ? 1f : 0f;
        var progress = (float)Math.Min(1.0, _clock.Elapsed.TotalMilliseconds / AnimationMilliseconds);

        // Ease out: quick at first, settling gently into place.
        var eased = 1 - (1 - progress) * (1 - progress) * (1 - progress);
        _position = _from + (target - _from) * eased;

        if (progress >= 1)
        {
            _position = target;
            _animation.Stop();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new RectangleF(
            Padding.Left + (Width - Padding.Horizontal - TrackWidth) / 2f + 0.5f,
            (Height - TrackHeight) / 2f + 0.5f,
            TrackWidth - 1,
            TrackHeight - 1);
        var radius = S(3f);

        // Off: a flat grey track. On: dark on the left fading to green on the
        // right. No outline either way. In between, one blends into the other.
        var start = Enabled ? SwitchGradientStart : SystemColors.GrayText;
        var end = Enabled ? RazerGreen : SystemColors.GrayText;

        using (var path = RoundedButton.RoundedPath(track, radius))
        {
            using (var fill = new LinearGradientBrush(RectangleF.Inflate(track, 1, 0),
                Blend(OffTrackColor, start, _position), Blend(OffTrackColor, end, _position), LinearGradientMode.Horizontal))
            {
                graphics.FillPath(fill, path);
            }

            // Only keyboard focus (Tab) shows a ring, as on the buttons.
            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Color.Silver);
                using var outer = RoundedButton.RoundedPath(RectangleF.Inflate(track, 3, 3), radius + 3);
                graphics.DrawPath(ring, outer);
            }
        }

        // The knob, a black square with softened corners, slides from left to right.
        var knob = TrackHeight - 2 * KnobInset;
        var left = track.Left + KnobInset;
        var right = track.Right - KnobInset - knob;
        var knobBounds = new RectangleF(left + (right - left) * _position, track.Top + (track.Height - knob) / 2f, knob, knob);

        using var knobFill = new SolidBrush(Color.Black);
        using var knobShape = RoundedButton.RoundedPath(knobBounds, S(2f));
        graphics.FillPath(knobFill, knobShape);
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animation.Dispose();

        base.Dispose(disposing);
    }
}
