using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The app's button: a filled shape with rounded corners and a 2.5px line along
/// the bottom only, which curls up around the bottom corners. WinForms' flat button can only draw square corners,
/// so this paints itself: the fill (a dark-to-green gradient when selected, a
/// little lighter while pressed), the bottom line (green when selected or
/// under the pointer), then the icon and text. A green BackColor is what marks
/// the selected button, so selecting or greying a button works exactly as with
/// a stock one.
/// </summary>
internal class RoundedButton : Button
{
    // Base-design pixels, scaled like everything else.
    internal const int CornerRadius = 6;

    private readonly System.Windows.Forms.Timer _hoverAnimation = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _hoverClock = new();

    private bool _hovered;
    private bool _pressed;

    // 0 at rest, 1 fully hovered; in between while the hover fades in or out.
    private float _hover;
    private float _hoverFrom;

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

        _hoverAnimation.Tick += (_, _) => StepHover();
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

        var parentColor = Parent?.BackColor ?? BackgroundColor;

        // Outside the corners the parent shows through.
        graphics.Clear(parentColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // The only outline is a line along the bottom: the button's whole
        // shape in the line's color, with the face laid over it a little
        // shorter, so the line shows under the face and curls up around its
        // bottom corners. Green on the selected button and on any other under
        // the pointer, quiet otherwise, silver while it has keyboard focus (Tab).
        var stroke = Focused && ShowFocusCues ? Color.Silver
            : IsGreen && IsUsable ? RazerGreen
            : Motion.Blend(ButtonBorderColor, RazerGreen, Hover);

        // Only its bottom part is painted: higher up the face's edge lies
        // exactly on it, and the line's color would fringe the face.
        using (var shape = RoundedPath(new RectangleF(0, 0, Width, Height), S(CornerRadius)))
        using (var line = new SolidBrush(stroke))
        {
            var lineTop = Height - ButtonStroke - S(CornerRadius);
            graphics.SetClip(new RectangleF(0, lineTop, Width, Height - lineTop));
            graphics.FillPath(line, shape);
            graphics.ResetClip();
        }

        using (var face = RoundedPath(new RectangleF(0, 0, Width, Height - ButtonStroke), S(CornerRadius)))
        {
            // A selected button fades from a faint green at the top to a soft
            // green at the bottom, stronger under the pointer, over the
            // parent's color (the greens are see-through); the others are flat.
            if (IsGreen)
            {
                using var under = new SolidBrush(parentColor);
                graphics.FillPath(under, face);
            }

            using Brush fill = IsGreen
                ? new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), Pressed(SelectedGradientStart), Pressed(Motion.Blend(SelectedGradientEnd, SelectedGradientHoverEnd, Hover)), LinearGradientMode.Vertical)
                : new SolidBrush(Pressed(BackColor));

            graphics.FillPath(fill, face);
        }

        var textBounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Width - Padding.Horizontal,
            Height - Padding.Vertical);

        // The text stays white on the selected button's dark gradient.
        var textColor = !Enabled ? SystemColors.GrayText
            : ForeColor == OnGreenTextColor ? Color.White
            : ForeColor;

        // The icon is green on the selected button, and turns green under the
        // pointer on the others.
        var glyphColor = IsGreen && IsUsable ? RazerGreen
            : IsUsable ? Motion.Blend(textColor, RazerGreen, Hover)
            : textColor;

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

                var stacked = new RectangleF(textBounds.Left + (textBounds.Width - StackedGlyphSize) / 2f, top, StackedGlyphSize, StackedGlyphSize);

                Glyphs.Draw(graphics, glyph, stacked, glyphColor);

                var textArea = new Rectangle(textBounds.Left, top + StackedGlyphSize + StackedGlyphGap, textBounds.Width, textHeight);
                TextRenderer.DrawText(graphics, Text, Font, textArea, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                return;
            }

            var textWidth = TextRenderer.MeasureText(graphics, Text, Font, textBounds.Size, TextFormatFlags.SingleLine).Width;
            var groupWidth = Math.Min(GlyphSize + GlyphGap + textWidth, textBounds.Width);
            var left = textBounds.Left + (textBounds.Width - groupWidth) / 2;

            var inline = new RectangleF(left, (Height - GlyphSize) / 2f, GlyphSize, GlyphSize);

            Glyphs.Draw(graphics, glyph, inline, glyphColor);

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

    /// <summary>The size of a glyph beside the text; smaller on the small buttons, to match their text.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int GlyphSize { get; set; } = S(16);

    /// <summary>The space between a glyph beside the text and the text.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int GlyphGap { get; set; } = S(6);

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        StartHover();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        StartHover();
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

    // Pressing lightens a button that can be used; one drawn as unavailable
    // (a hand cursor is what marks the usable ones) stays flat. Hover changes
    // only the bottom line and the icon, never the fill.
    private Color Pressed(Color color) => _pressed && IsUsable ? Lighten(color, 0.12f) : color;

    private bool IsGreen => BackColor.ToArgb() == RazerGreen.ToArgb();

    private bool IsUsable => Enabled && Cursor == Cursors.Hand;

    // The hover, eased; nothing for a button that cannot be used.
    private float Hover => IsUsable ? Motion.Ease(_hover) : 0;

    // Fades the hover in or out over Motion.Milliseconds, from wherever it is now.
    private void StartHover()
    {
        _hoverFrom = _hover;
        _hoverClock.Restart();
        _hoverAnimation.Start();
        Invalidate();
    }

    private void StepHover()
    {
        var target = _hovered ? 1f : 0f;

        // The whole way takes Motion.Milliseconds; a part of the way, its share.
        var needed = Math.Abs(target - _hoverFrom) * Motion.Milliseconds;
        var done = needed <= 0 ? 1 : Math.Min(1, _hoverClock.Elapsed.TotalMilliseconds / needed);
        _hover = _hoverFrom + (target - _hoverFrom) * (float)done;

        if (done >= 1)
            _hoverAnimation.Stop();

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hoverAnimation.Dispose();

        base.Dispose(disposing);
    }

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
