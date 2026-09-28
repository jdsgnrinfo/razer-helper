using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// An on/off switch as Windows 11 Settings draws it: a pill with a knob that
/// sits right when on. On is Razer green with a dark knob; off is a quiet
/// outline with a light one. A change slides the knob across and fades the
/// colors rather than jumping. Everything else (Checked, events, Space to
/// toggle) is the ordinary CheckBox.
/// </summary>
internal sealed class ToggleSwitch : CheckBox
{
    private const double AnimationMilliseconds = 160;

    private static int TrackWidth => S(40);
    private static int TrackHeight => S(20);
    private static int KnobInset => S(4);

    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();

    // 0 is fully off, 1 fully on; in between while the knob slides.
    private float _position;
    private float _from;
    private bool _hovered;

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
        new(TrackWidth + S(6) + Padding.Horizontal, TrackHeight + S(8) + Padding.Vertical);

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
        var radius = track.Height / 2f;
        var on = Enabled ? RazerGreen : SystemColors.GrayText;

        using (var path = RoundedButton.RoundedPath(track, radius))
        {
            // The outline fades out as the green fades in.
            using var outline = new Pen(Blend(_hovered && Enabled ? Color.White : Color.Silver, on, _position), S(1.5f));
            graphics.DrawPath(outline, path);

            if (_position > 0)
            {
                using var fill = new SolidBrush(Color.FromArgb((int)(255 * _position), on));
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

        // The knob slides from left to right, light turning dark, and is a
        // little larger under the pointer, as in Windows.
        var knob = TrackHeight - 2 * KnobInset + (_hovered && Enabled ? S(2) : 0);
        var left = track.Left + KnobInset - (_hovered ? S(1) : 0);
        var right = track.Right - KnobInset - knob + (_hovered ? S(1) : 0);
        var knobBounds = new RectangleF(left + (right - left) * _position, track.Top + (track.Height - knob) / 2f, knob, knob);

        using var knobFill = new SolidBrush(Blend(Enabled ? Color.Silver : SystemColors.GrayText, BackgroundColor, _position));
        graphics.FillEllipse(knobFill, knobBounds);
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animation.Dispose();

        base.Dispose(disposing);
    }
}
