using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The window's X, at its top right: grey at rest, and under the pointer a
/// red rounded square with the X in white, as Windows' own close buttons.
/// </summary>
internal sealed class WindowCloseButton : Control
{
    private static readonly Color HoverColor = Color.FromArgb(0xE8, 0x11, 0x23);

    private static int GlyphSize => S(16);

    private bool _hovered;

    public WindowCloseButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.PushButton;
        Cursor = Cursors.Hand;
        Size = new Size(S(34), S(34));
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_hovered)
        {
            using var fill = new SolidBrush(HoverColor);
            using var path = RoundedButton.RoundedPath(new RectangleF(0, 0, Width, Height), S(RoundedButton.CornerRadius));
            graphics.FillPath(fill, path);
        }

        Glyphs.Draw(graphics, Glyph.Close, new RectangleF((Width - GlyphSize) / 2f, (Height - GlyphSize) / 2f, GlyphSize, GlyphSize),
            _hovered ? Color.White : SubtleTextColor);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}
