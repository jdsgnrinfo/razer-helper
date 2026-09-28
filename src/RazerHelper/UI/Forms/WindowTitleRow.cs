using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The top of the small windows beside the popup (Settings, Custom): the title
/// in green on the left, the close X on the right, as in the popup.
/// </summary>
internal static class WindowTitleRow
{
    /// <param name="title">English text; translated here.</param>
    public static Control Create(Form window, string title, int width)
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Height = S(30),
            Margin = S(new Padding(0, 0, 0, 10)),
            Padding = Padding.Empty,
            RowCount = 1,
            Width = width
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        row.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = GetDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Margin = Padding.Empty,
            Text = L.T(title)
        }, 0, 0);

        var close = new GlyphButton(Glyph.Close, S(18))
        {
            AccessibleName = L.T("Close"),
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = S(new Size(28, 28))
        };

        close.Click += (_, _) => window.Close();
        row.Controls.Add(close, 1, 0);
        return row;
    }
}
