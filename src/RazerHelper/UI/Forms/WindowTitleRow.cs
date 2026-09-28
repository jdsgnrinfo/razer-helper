using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The top of the small windows beside the popup (Settings, Custom): the
/// icon and the title in bold white on the left, the close X on the right.
/// </summary>
internal static class WindowTitleRow
{
    /// <summary>The row itself; what follows it sets its own gap above.</summary>
    public static int Height => S(24);

    /// <param name="title">English text; translated here.</param>
    public static Control Create(Form window, string title, Glyph icon, int width)
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Height = Height,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            Width = width
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var heading = UiControls.CreateSectionLabel(title, icon);
        heading.BackColor = BackgroundColor;
        heading.Font = DesignFont(13, FontStyle.Bold);
        row.Controls.Add(heading, 0, 0);

        var close = new GlyphButton(Glyph.Close, S(18))
        {
            AccessibleName = L.T("Close"),
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = S(new Size(24, 24))
        };

        close.Click += (_, _) => window.Close();
        row.Controls.Add(close, 1, 0);
        return row;
    }
}
