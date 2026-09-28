using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A table layout drawn as a card: its BackColor filled with the buttons'
/// rounded corners, on the parent's color. Children should use the card's
/// BackColor so they sit on it seamlessly.
/// </summary>
internal sealed class CardPanel : TableLayoutPanel
{
    public CardPanel()
    {
        BackColor = CardColor;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var fill = new SolidBrush(BackColor);
        using var shape = RoundedButton.RoundedPath(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), S(RoundedButton.CornerRadius));
        graphics.FillPath(fill, shape);
    }
}
