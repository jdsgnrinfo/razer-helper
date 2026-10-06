namespace RazerHelper.UI;

/// <summary>
/// A label for a slider's number (the charge limit, a brightness, a boost
/// level): when its text changes, the digits that changed roll to the new
/// ones, the way the value moved. Laid out like a label: its font, color and
/// TextAlign's left, centre or right, vertically centred.
/// </summary>
internal sealed class RollingLabel : Label
{
    private readonly RollingText _text = new();
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };

    public RollingLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _frames.Tick += (_, _) =>
        {
            if (!_text.Rolling)
                _frames.Stop();

            Invalidate();
        };
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        // Only a label on screen rolls; one filled in before it shows just takes the text.
        _text.Set(Text, animate: IsHandleCreated && Visible);

        if (_text.Rolling)
            _frames.Start();

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        // A see-through label takes its parent's color, since fading digits blend into it.
        var back = BackColor.A == 255 ? BackColor : Parent?.BackColor ?? Color.Black;
        graphics.Clear(back);

        var alignment = TextAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => HorizontalAlignment.Left,
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right
        };

        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        _text.Paint(
            graphics,
            new RectangleF(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical),
            alignment,
            text => TextRenderer.MeasureText(graphics, text, Font, Size.Empty, Flags),
            // GDI text has no opacity, so a fading digit is blended into the background instead.
            (text, at, opacity) => TextRenderer.DrawText(graphics, text, Font, Point.Round(at), Motion.Blend(back, ForeColor, Math.Clamp(opacity, 0, 1)), Flags));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }
}
