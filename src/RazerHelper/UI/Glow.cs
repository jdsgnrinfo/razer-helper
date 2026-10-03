using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The soft green glow around a selected button. A control cannot paint
/// outside itself, so the row holding the buttons paints it (see
/// <see cref="Attach"/>), over the gaps and its own margin; and each glowing
/// button first paints the part that falls on it (<see cref="PaintBehind"/>),
/// so the glow runs on under a neighbour's corners as well.
/// </summary>
internal static class Glow
{
    /// <summary>How far the glow spreads from a button's edge.</summary>
    public static int Radius => UiControls.GlowRoom;

    // At the button's edge; it fades to nothing at the radius.
    private const int EdgeAlpha = 150;

    /// <summary>Has <paramref name="row"/> paint the glow of its selected buttons.</summary>
    public static void Attach(Control row) => row.Paint += (_, e) => PaintAround(e.Graphics, row);

    /// <summary>Paints, in the button's own coordinates, the glow of its row that falls on it.</summary>
    public static void PaintBehind(Graphics graphics, RoundedButton button)
    {
        if (button.Parent is not { } row)
            return;

        var state = graphics.Save();
        graphics.TranslateTransform(-button.Left, -button.Top);
        PaintAround(graphics, row);
        graphics.Restore(state);
    }

    private static void PaintAround(Graphics graphics, Control row)
    {
        foreach (var button in row.Controls.OfType<RoundedButton>())
        {
            if (!button.Visible || button.GlowRoom <= 0 || !button.IsGreen)
                continue;

            var body = button.Body;
            body.Offset(button.Left, button.Top);
            Paint(graphics, body);
        }
    }

    // Rings of green a pixel apart, strongest at the edge and fading out
    // smoothly (eased), from the outside in.
    private static void Paint(Graphics graphics, RectangleF body)
    {
        var smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var radius = Radius;

        for (var step = radius; step >= 1; step--)
        {
            var fade = 1f - (step - 0.5f) / radius;
            var alpha = (int)(EdgeAlpha * fade * fade * fade);

            if (alpha <= 0)
                continue;

            using var pen = new Pen(Color.FromArgb(alpha, RazerGreen), 1.5f);
            using var ring = RoundedButton.RoundedPath(RectangleF.Inflate(body, step - 0.5f, step - 0.5f), S(RoundedButton.CornerRadius) + step);
            graphics.DrawPath(pen, ring);
        }

        graphics.SmoothingMode = smoothing;
    }
}
