using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A check box in the app's style. The stock one is always drawn in Windows'
/// accent blue; this one is Razer green with a dark tick when checked and a
/// quiet outline when not, 2px larger than the stock box, with slightly
/// rounded corners like the buttons. Everything else (Checked, events,
/// keyboard use) is the ordinary CheckBox.
/// </summary>
internal sealed class ThemedCheckBox : CheckBox
{
    private static int BoxSize => S(15);
    private static int BoxGap => S(7);
    private static float CornerRadius => S(3);

    private bool _hovered;

    public ThemedCheckBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(
            BoxSize + BoxGap + text.Width + Padding.Horizontal,
            Math.Max(BoxSize, text.Height) + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var box = new RectangleF(Padding.Left + 0.5f, (Height - BoxSize) / 2f + 0.5f, BoxSize - 1, BoxSize - 1);

        using (var path = RoundedButton.RoundedPath(box, CornerRadius))
        {
            if (Checked)
            {
                using var fill = new SolidBrush(Enabled ? RazerGreen : SystemColors.GrayText);
                graphics.FillPath(fill, path);

                // The tick, dark on green like a selected button's text.
                using var tick = new Pen(BackgroundColor, S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                graphics.DrawLines(tick,
                [
                    new PointF(box.Left + box.Width * 0.24f, box.Top + box.Height * 0.52f),
                    new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.71f),
                    new PointF(box.Left + box.Width * 0.77f, box.Top + box.Height * 0.31f)
                ]);
            }
            else
            {
                using var outline = new Pen(_hovered && Enabled ? Color.Silver : Color.Gray, S(1.5f));
                graphics.DrawPath(outline, path);
            }

            // Only keyboard focus (Tab) shows a ring, as on the buttons.
            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Color.Silver);
                using var outer = RoundedButton.RoundedPath(RectangleF.Inflate(box, 2, 2), CornerRadius + 2);
                graphics.DrawPath(ring, outer);
            }
        }

        var textLeft = Padding.Left + BoxSize + BoxGap;
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            new Rectangle(textLeft, 0, Width - textLeft, Height),
            Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

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
}
