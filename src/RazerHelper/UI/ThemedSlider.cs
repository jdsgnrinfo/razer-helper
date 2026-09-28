using System.ComponentModel;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark, flat slider that snaps to fixed steps. The stock TrackBar ignores
/// the theme, so this draws the track and thumb itself.
/// </summary>
/// <remarks>
/// <see cref="ValueChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> fires once when the user lets go (mouse up, or a
/// key that moved the value), which is the moment to act on the value.
/// </remarks>
internal sealed class ThemedSlider : Control
{
    private static int ThumbRadius => S(8);
    private static int TrackHeight => S(5);

    private static readonly Color DisabledColor = Color.FromArgb(100, 100, 100);

    // Drawing objects shared by every slider and reused on every repaint. A
    // slider repaints on each mouse move while dragging, so building these each
    // time was steady garbage for nothing.
    private static readonly SolidBrush TrackBrush = new(TrackColor);
    private static readonly SolidBrush AccentBrush = new(RazerGreen);
    private static readonly SolidBrush DisabledBrush = new(DisabledColor);
    private static readonly Pen FocusRingPen = new(Color.White, 2);

    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _step;

    private int _value;
    private bool _dragging;
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
    }

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
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
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
        Value = ValueAt(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
            Value = ValueAt(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        _dragging = false;
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
        Value = target.Value;
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

        var accent = IsLive ? AccentBrush : DisabledBrush;
        var thumbX = XAt(_value);

        // Track, the full width with round ends, then the filled part up to the thumb.
        var top = TrackY - TrackHeight / 2f;
        FillPill(graphics, TrackBrush, new RectangleF(0, top, Width - 1, TrackHeight));
        FillPill(graphics, accent, new RectangleF(0, top, thumbX, TrackHeight));

        // Thumb: a green dot, with a white ring when the keyboard has focus.
        var thumb = new Rectangle(thumbX - ThumbRadius, TrackY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);

        graphics.FillEllipse(accent, thumb);

        if (Focused && IsLive)
            graphics.DrawEllipse(FocusRingPen, thumb);
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
