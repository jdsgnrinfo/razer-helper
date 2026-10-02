using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A button that is only an icon: no box, just the glyph, quiet until the
/// pointer is over it and then in the app's green. The clickable area is the
/// whole control, which can be larger than the glyph so it is easy to hit.
/// </summary>
internal sealed class GlyphButton : Control
{
    // White at a little over half strength: present but not loud.
    private static readonly Color RestingColor = Color.FromArgb(150, Color.White);

    private readonly Glyph _glyph;
    private readonly int _glyphSize;
    private bool _hovered;

    public GlyphButton(Glyph glyph, int glyphSize)
    {
        _glyph = glyph;
        _glyphSize = glyphSize;

        AccessibleRole = AccessibleRole.PushButton;
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Glass.Clear(e.Graphics, Parent?.BackColor ?? BackgroundColor);

        Glyphs.Draw(
            e.Graphics,
            _glyph,
            new RectangleF((Width - _glyphSize) / 2f, (Height - _glyphSize) / 2f, _glyphSize, _glyphSize),
            _hovered ? RazerGreen : RestingColor);
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
