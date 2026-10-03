using System.Runtime.InteropServices;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A tooltip in the app's dark theme, styled like the drop-down lists: the
/// same background, the same 9.5pt text, no outline and 4px rounded corners.
/// Windows' stock tooltip is a pale yellow box in the system font, which
/// clashes with the popup, so this one draws itself.
/// </summary>
internal sealed class ThemedToolTip : ToolTip
{
    private static readonly Font TipFont = GetDesignFont(FontFamilyName, 10F);
    private static readonly Padding TextPadding = new(S(10), S(6), S(10), S(6));
    private static readonly SolidBrush BackgroundBrush = new(ButtonColor);

    // The tooltip's own window, once it has been given its rounded corners.
    private IntPtr _roundedWindow;

    public ThemedToolTip()
    {
        OwnerDraw = true;

        // The popup is a tray window that is often not the active one.
        ShowAlways = true;

        Popup += ThemedToolTip_Popup;
        Draw += ThemedToolTip_Draw;
    }

    // Size the tip to its text, so a short message gives a small box.
    private void ThemedToolTip_Popup(object? sender, PopupEventArgs e)
    {
        var text = e.AssociatedControl is null ? string.Empty : GetToolTip(e.AssociatedControl);
        var textSize = TextRenderer.MeasureText(text, TipFont, Size.Empty, TextFormatFlags.NoPadding);

        e.ToolTipSize = new Size(
            textSize.Width + TextPadding.Horizontal,
            textSize.Height + TextPadding.Vertical);
    }

    private void ThemedToolTip_Draw(object? sender, DrawToolTipEventArgs e)
    {
        RoundTheWindow(e.Graphics);

        // No outline: Windows 11 rounds the corners (see RoundedWindow).
        e.Graphics.FillRectangle(BackgroundBrush, e.Bounds);

        // A short message reads best centered; a list of lines reads best from the left.
        var alignment = e.ToolTipText?.Contains('\n') == true ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;

        TextRenderer.DrawText(
            e.Graphics,
            e.ToolTipText,
            TipFont,
            new Rectangle(
                e.Bounds.X + TextPadding.Left,
                e.Bounds.Y,
                e.Bounds.Width - TextPadding.Horizontal,
                e.Bounds.Height),
            Color.White,
            alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // The ToolTip component does not expose its window, but the window being
    // painted is the one behind the drawing surface. Windows 11 then rounds it
    // (4px) and drops its outline, as it does for the drop-down lists; once
    // per window, since the tooltip reuses it.
    private void RoundTheWindow(Graphics graphics)
    {
        var hdc = graphics.GetHdc();

        try
        {
            var window = WindowFromDC(hdc);

            if (window == IntPtr.Zero || window == _roundedWindow)
                return;

            RoundedWindow.Apply(window);
            _roundedWindow = window;
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromDC(IntPtr hdc);
}
