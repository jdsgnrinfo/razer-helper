using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A thin outline in the dividers' color around the main window and the
/// windows beside it, drawn by the app on the panel that fills the window.
/// Windows 11 can draw a window outline itself, but on these borderless
/// windows it does not always do so.
/// </summary>
internal static class WindowOutline
{
    // Windows 11 rounds a window's corners by 8px at 100%; Windows 10 leaves them square.
    private const float WindowsCornerRadius = 8f;

    private static bool CornersAreRounded => Environment.OSVersion.Version.Build >= 22000;

    /// <param name="surface">The panel that fills the window; its padding keeps the edge clear.</param>
    public static void Attach(Control surface)
    {
        surface.Paint += (_, e) => Draw(e.Graphics, surface);
        surface.Resize += (_, _) => surface.Invalidate();
    }

    private static void Draw(Graphics graphics, Control surface)
    {
        var width = S(1f);
        var inset = width / 2;
        var bounds = new RectangleF(inset, inset, surface.Width - width, surface.Height - width);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(DividerColor, width);

        if (!CornersAreRounded)
        {
            graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            return;
        }

        var radius = WindowsCornerRadius * surface.DeviceDpi / 96f - inset;
        using var path = RoundedButton.RoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }
}
