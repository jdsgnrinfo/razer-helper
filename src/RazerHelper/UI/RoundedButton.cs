using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The app's button: a flat fill with rounded corners, green with dark text
/// when selected and grey with white text otherwise, a touch lighter under the
/// pointer and lighter still while pressed. WinForms' flat button can only draw
/// square corners, so this paints itself. A green BackColor is what marks the
/// selected button, so selecting or greying a button works exactly as with a
/// stock one.
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

        // Outside the corners the parent shows through.
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var fillColor = IsGreen
            ? Motion.Blend(RazerGreen, RazerGreenHover, Hover)
            : BackColor == ButtonColor ? Motion.Blend(ButtonColor, ButtonHoverColor, Hover) : BackColor;

        using (var path = RoundedPath(new RectangleF(0, 0, Width, Height), S(CornerRadius)))
        {
            using (var fill = new SolidBrush(Pressed(fillColor)))
                graphics.FillPath(fill, path);

            // Keyboard focus (Tab) shows a thin silver outline.
            if (Focused && ShowFocusCues)
            {
                using var ring = RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), S(CornerRadius));
                using var pen = new Pen(Color.Silver);
                graphics.DrawPath(pen, ring);
            }
        }

        var textBounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Width - Padding.Horizontal,
            Height - Padding.Vertical);

        var textColor = !Enabled ? SystemColors.GrayText : ForeColor;

        // With a glyph: a larger icon in a circle over the text, the pair
        // centered in the button. The icon is green on grey, dark on the selected
        // green (whose circle is a deeper green), grey when unavailable.
        // With an icon of the caller's: just the icon, centred, in the text's
        // colour; the name is left to a tooltip.
        if (Icon is { } painter)
        {
            var height = S(IconOnlyHeight);
            painter(graphics, new RectangleF(textBounds.Left, textBounds.Top + (textBounds.Height - height) / 2f, textBounds.Width, height), textColor);
            return;
        }

        if (Glyph is not null)
        {
            var textHeight = TextRenderer.MeasureText(graphics, Text, Font, textBounds.Size, TextFormatFlags.SingleLine).Height;
            var top = textBounds.Top + (textBounds.Height - (CircleSize + StackedGlyphGap + textHeight)) / 2;
            var circle = new RectangleF(textBounds.Left + (textBounds.Width - CircleSize) / 2f, top, CircleSize, CircleSize);

            using (var circleFill = new SolidBrush(IsGreen ? SelectedIconCircleColor : IconCircleColor))
                graphics.FillEllipse(circleFill, circle);

            var iconColor = !IsUsable ? textColor : IsGreen ? OnGreenTextColor : RazerGreen;
            var icon = RectangleF.Inflate(circle, -(CircleSize - StackedGlyphSize) / 2f, -(CircleSize - StackedGlyphSize) / 2f);
            Glyphs.Draw(graphics, Glyph.Value, icon, iconColor);

            var textArea = new Rectangle(textBounds.Left, top + CircleSize + StackedGlyphGap, textBounds.Width, textHeight);
            TextRenderer.DrawText(graphics, Text, Font, textArea, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
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

    /// <summary>An optional icon in a circle over the text, as on the performance mode buttons.</summary>
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

    /// <summary>An icon the caller draws in place of the text, on its own (the refresh rates).</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<Graphics, RectangleF, Color>? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            Invalidate();
        }
    }

    private Action<Graphics, RectangleF, Color>? _icon;

    // Base-design pixels: the height of an icon shown without text.
    private const int IconOnlyHeight = 18;

    private static int StackedGlyphSize => S(30);
    private static int CircleSize => S(64);
    private static int StackedGlyphGap => S(8);

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
    // (a hand cursor is what marks the usable ones) stays flat.
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
