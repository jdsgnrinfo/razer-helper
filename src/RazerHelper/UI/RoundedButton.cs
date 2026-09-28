using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The app's button: a filled shape with rounded corners and no outline.
/// WinForms' flat button can only draw square corners, so this paints itself:
/// the fill (a little lighter on hover, lighter still while pressed), then
/// the text. Colors come from the ordinary BackColor and ForeColor, so
/// selecting or greying a button works exactly as with a stock one.
/// </summary>
internal class RoundedButton : Button
{
    // Base-design pixels, scaled like everything else.
    internal const int CornerRadius = 2;

    private bool _hovered;
    private bool _pressed;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>A rounded rectangle filling <paramref name="bounds"/>.</summary>
    internal static GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;

        // Outside the corners the parent shows through.
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var fill = new SolidBrush(CurrentFill()))
        using (var path = RoundedPath(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), S(CornerRadius)))
        {
            graphics.FillPath(fill, path);

            // An unselected button that can be used gets a green outline under
            // the pointer, its fill unchanged, so it reads as "can be picked"
            // without looking already selected.
            if (ShowsGreenOutline)
            {
                using var outline = new Pen(RazerGreen, S(1.5f));
                using var inner = RoundedPath(new RectangleF(0.75f, 0.75f, Width - 2f, Height - 2f), S(CornerRadius) - 0.75f);
                graphics.DrawPath(outline, inner);
            }
            // Otherwise a quiet 1px outline on an unselected button (a green
            // one has none), silver while it has keyboard focus (Tab) so focus
            // is never lost.
            else if (!IsGreen || (Focused && ShowFocusCues))
            {
                using var ring = new Pen(Focused && ShowFocusCues ? Color.Silver : ButtonBorderColor);
                using var inner = RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), S(CornerRadius));
                graphics.DrawPath(ring, inner);
            }
        }

        var textBounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Width - Padding.Horizontal,
            Height - Padding.Vertical);

        var textColor = Enabled ? ForeColor : SystemColors.GrayText;

        // With a glyph, the glyph and the text are centered together as one
        // group, the glyph drawn in the text's color so it follows selection
        // and availability with it.
        if (Glyph is { } glyph)
        {
            if (GlyphAbove)
            {
                // A larger glyph over the text, the pair centered in the button.
                var textHeight = TextRenderer.MeasureText(graphics, Text, Font, textBounds.Size, TextFormatFlags.SingleLine).Height;
                var top = textBounds.Top + (textBounds.Height - (StackedGlyphSize + StackedGlyphGap + textHeight)) / 2;

                Glyphs.Draw(
                    graphics,
                    glyph,
                    new RectangleF(textBounds.Left + (textBounds.Width - StackedGlyphSize) / 2f, top, StackedGlyphSize, StackedGlyphSize),
                    textColor);

                var textArea = new Rectangle(textBounds.Left, top + StackedGlyphSize + StackedGlyphGap, textBounds.Width, textHeight);
                TextRenderer.DrawText(graphics, Text, Font, textArea, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                return;
            }

            var textWidth = TextRenderer.MeasureText(graphics, Text, Font, textBounds.Size, TextFormatFlags.SingleLine).Width;
            var groupWidth = Math.Min(GlyphSize + GlyphGap + textWidth, textBounds.Width);
            var left = textBounds.Left + (textBounds.Width - groupWidth) / 2;

            Glyphs.Draw(graphics, glyph, new RectangleF(left, (Height - GlyphSize) / 2f, GlyphSize, GlyphSize), textColor);

            textBounds = new Rectangle(left + GlyphSize + GlyphGap, textBounds.Top, groupWidth - GlyphSize - GlyphGap, textBounds.Height);
            TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            return;
        }

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            textColor,
            ToTextFlags(TextAlign) | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    /// <summary>An optional icon drawn before the text, for example on the performance mode buttons.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Glyph? Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            Invalidate();
        }
    }

    private Glyph? _glyph;

    /// <summary>Draws the glyph larger, above the text, instead of beside it.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool GlyphAbove
    {
        get => _glyphAbove;
        set
        {
            _glyphAbove = value;
            Invalidate();
        }
    }

    private bool _glyphAbove;

    private static int StackedGlyphSize => S(24);
    private static int StackedGlyphGap => S(6);

    private static int GlyphSize => S(16);
    private static int GlyphGap => S(6);

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    // Hover and press only change a button that can be used; one drawn as
    // unavailable (a hand cursor is what marks the usable ones) stays flat.
    private Color CurrentFill()
    {
        if (!IsUsable)
            return BackColor;

        if (_pressed)
            return Lighten(BackColor, 0.22f);

        // Only a green (selected) button lightens under the pointer; the others
        // keep their fill and show a green outline instead.
        return _hovered && IsGreen ? Lighten(BackColor, 0.12f) : BackColor;
    }

    private bool IsGreen => BackColor.ToArgb() == RazerGreen.ToArgb();

    private bool IsUsable => Enabled && Cursor == Cursors.Hand;

    private bool ShowsGreenOutline => _hovered && IsUsable && !IsGreen;

    private static Color Lighten(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R + (255 - color.R) * amount),
        (int)(color.G + (255 - color.G) * amount),
        (int)(color.B + (255 - color.B) * amount));

    private static TextFormatFlags ToTextFlags(ContentAlignment alignment)
    {
        var flags = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => TextFormatFlags.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };

        return flags | alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => TextFormatFlags.Top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
            _ => TextFormatFlags.VerticalCenter
        };
    }
}
