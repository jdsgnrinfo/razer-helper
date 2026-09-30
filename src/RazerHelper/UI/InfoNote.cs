using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A short note in a dark green box with a thin green edge: a green circle
/// with an exclamation mark, then the text under it in white. It sizes its
/// height to the text, which wraps within the given width.
/// </summary>
internal sealed class InfoNote : Control
{
    private static readonly Color FillColor = Color.FromArgb(0x18, 0x24, 0x16);
    private static readonly Color EdgeColor = Color.FromArgb(0x1E, 0x3D, 0x19);
    private static readonly Font TextFont = SemiBoldFont(14);
    private static readonly Font MarkFont = DesignFont(14, FontStyle.Bold);

    private static int Inset => S(16);
    private static int MarkSize => S(24);
    private static int MarkGap => S(12);

    private const TextFormatFlags Wrapped = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top;

    public InfoNote(string text, int width)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        Text = text;
        AccessibleName = text;

        var textHeight = TextRenderer.MeasureText(text, TextFont, new Size(width - 2 * Inset, int.MaxValue), Wrapped).Height;
        Size = new Size(width, Inset + MarkSize + MarkGap + textHeight + Inset);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var box = RoundedButton.RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), S(RoundedButton.CornerRadius)))
        using (var fill = new SolidBrush(FillColor))
        using (var edge = new Pen(EdgeColor))
        {
            graphics.FillPath(fill, box);
            graphics.DrawPath(edge, box);
        }

        var mark = new Rectangle(Inset, Inset, MarkSize, MarkSize);

        using (var circle = new SolidBrush(RazerGreen))
            graphics.FillEllipse(circle, mark);

        TextRenderer.DrawText(graphics, "!", MarkFont, mark, OnGreenTextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var textTop = Inset + MarkSize + MarkGap;
        TextRenderer.DrawText(graphics, Text, TextFont, new Rectangle(Inset, textTop, Width - 2 * Inset, Height - textTop - Inset), Color.White, Wrapped);
    }
}
