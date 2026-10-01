using System.ComponentModel;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark, flat slider that snaps to fixed steps: a thin track filling in green,
/// and a white square thumb. The stock TrackBar ignores
/// the theme, so this draws the track and thumb itself.
/// While dragging, the thumb follows the pointer freely and the value moves
/// in steps under it; on release, or after a click or a key, the thumb glides
/// to the step instead of jumping there.
/// </summary>
/// <remarks>
/// <see cref="ValueChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> fires once when the user lets go (mouse up, or a
/// key that moved the value), which is the moment to act on the value.
/// </remarks>
internal sealed class ThemedSlider : Control
{
    private static int ThumbRadius => S(8);
    private static int TrackHeight => S(6);

    // Unavailable: the filled part is a mid grey instead of green.
    private static readonly Color DisabledFillColor = Color.FromArgb(0x78, 0x78, 0x78);

    // Drawing objects shared by every slider and reused on every repaint. A
    // slider repaints on each mouse move while dragging, so building these each
    // time was steady garbage for nothing.
    private static readonly SolidBrush TrackBrush = new(TrackColor);
    private static readonly SolidBrush FillBrush = new(RazerGreen);
    private static readonly SolidBrush DisabledFillBrush = new(DisabledFillColor);
    private static readonly SolidBrush ThumbBrush = new(Color.White);
    private static readonly Pen FocusRingPen = new(RazerGreen, 2);

    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _step;

    private int _value;
    private bool _dragging;

    // Where the thumb is drawn. It follows the pointer while dragging and
    // eases towards the value's place otherwise.
    private float _thumbX = float.NaN;
    // How far from the thumb's centre it was grabbed, so it does not jump
    // under the pointer.
    private float _grabOffset;
    private readonly System.Windows.Forms.Timer _glide = new() { Interval = 15 };
    private bool _keyMovedValue;

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
        Height = S(20);
        Cursor = Cursors.Hand;
        _glide.Tick += (_, _) => GlideStep();
    }

    /// <summary>True while the user is moving it, with the mouse or a held key.</summary>
    public bool IsBeingMoved => _dragging || _keyMovedValue;

    public event EventHandler? ValueChanged;

    public event EventHandler? Committed;

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
            _available = value;
            TabStop = value;
            _dragging = false;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            SettleThumb();
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
            var snapped = Snap(value);

            if (snapped == _value)
                return;

            _value = snapped;

            // Set from outside (a refresh, say), the thumb goes straight there.
            if (!_dragging && !_glide.Enabled)
                SettleThumb();

            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sets the value from outside and lets the thumb glide there, unless the user is moving it.</summary>
    public void GlideTo(int value)
    {
        if (IsBeingMoved)
            return;

        _thumbX = CurrentThumbX;
        _glide.Start(); // So the value's setter leaves the thumb to glide.
        Value = value;
        GlideToValue();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End ||
        base.IsInputKey(keyData);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || !IsLive)
            return;

        Focus();
        _dragging = true;
        _glide.Stop();

        // Grabbing the thumb keeps it where it is under the pointer; a click on
        // the track brings it to the pointer.
        var thumbX = CurrentThumbX;
        _grabOffset = Math.Abs(e.X - thumbX) <= ThumbRadius + S(2) ? e.X - thumbX : 0;
        DragTo(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
            DragTo(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        _dragging = false;
        GlideToValue();
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsLive)
            return;

        var target = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - _step,
            Keys.Right or Keys.Up => _value + _step,
            Keys.Home => _minimum,
            Keys.End => _maximum,
            _ => (int?)null
        };

        if (target is null)
            return;

        e.Handled = true;

        var before = _value;
        // The thumb glides from where it is; starting the timer first keeps the
        // value's setter from putting it straight in place.
        _thumbX = CurrentThumbX;
        _glide.Start();
        Value = target.Value;
        GlideToValue();
        _keyMovedValue |= _value != before;
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var thumbX = CurrentThumbX;

        // Track, the full width with round ends, then the filled part up to the thumb.
        var top = TrackY - TrackHeight / 2f;
        FillPill(graphics, TrackBrush, new RectangleF(0, top, Width - 1, TrackHeight));
        // The filled part is green up to the thumb, grey while the slider cannot be used.
        FillPill(graphics, IsLive ? FillBrush : DisabledFillBrush, new RectangleF(0, top, thumbX, TrackHeight));

        // Thumb: a white square with softened corners,
        // with a green ring when the keyboard has focus.
        var thumb = new RectangleF(thumbX - ThumbRadius, TrackY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);

        using (var thumbShape = RoundedButton.RoundedPath(thumb, S(2f)))
            graphics.FillPath(ThumbBrush, thumbShape);

        if (Focused && IsLive)
        {
            using var ring = RoundedButton.RoundedPath(RectangleF.Inflate(thumb, 2, 2), S(3f));
            graphics.DrawPath(FocusRingPen, ring);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SettleThumb();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _glide.Dispose();

        base.Dispose(disposing);
    }

    private float CurrentThumbX => float.IsNaN(_thumbX) ? XAt(_value) : _thumbX;

    // The thumb under the pointer, kept on the track; the value takes the nearest step.
    private void DragTo(int x)
    {
        _thumbX = Math.Clamp(x - _grabOffset, ThumbRadius, ThumbRadius + TrackWidth);
        Value = ValueAt((int)Math.Round(_thumbX));
        Invalidate();
    }

    private void SettleThumb()
    {
        _glide.Stop();
        _thumbX = float.NaN;
        Invalidate();
    }

    private void GlideToValue()
    {
        if (float.IsNaN(_thumbX) || Math.Abs(_thumbX - XAt(_value)) < 0.5f)
        {
            SettleThumb();
            return;
        }

        _glide.Start();
    }

    // Each tick covers a share of what is left, so the thumb slows as it lands.
    private void GlideStep()
    {
        var target = XAt(_value);
        _thumbX += (target - _thumbX) * 0.35f;

        if (Math.Abs(target - _thumbX) < 0.5f)
            SettleThumb();
        else
            Invalidate();
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

    private int TrackWidth => Math.Max(1, Width - ThumbRadius * 2 - 1);

    private int XAt(int value) =>
        ThumbRadius + (int)Math.Round((value - _minimum) / (double)(_maximum - _minimum) * TrackWidth);

    private int ValueAt(int x)
    {
        var fraction = Math.Clamp((x - ThumbRadius) / (double)TrackWidth, 0, 1);
        return Snap(_minimum + (int)Math.Round(fraction * (_maximum - _minimum)));
    }

    private int Snap(int value)
    {
        var clamped = Math.Clamp(value, _minimum, _maximum);
        return _minimum + (int)Math.Round((clamped - _minimum) / (double)_step, MidpointRounding.AwayFromZero) * _step;
    }
}
