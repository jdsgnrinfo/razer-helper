using RazerHelper.UI.Pages;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Custom mode's levels, opened under the mode buttons while the laptop is in
/// Custom: the CPU's on the left and the GPU's on the right. Each click
/// applies at once. The selectors belong to the Performance section, which
/// keeps them up to date.
/// </summary>
internal sealed class CustomBoostPanel : TableLayoutPanel
{
    private static int ColumnGap => S(24);

    private static int ColumnWidth => (PageView.ContentWidth - ColumnGap) / 2;

    /// <summary>How tall the panel is.</summary>
    public static int PanelHeight => CustomBoostSelectors.SelectorHeight;

    public CustomBoostPanel(CustomBoostSelectors selectors)
    {
        BackColor = BackgroundColor;
        ColumnCount = 2;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 1;
        Size = new Size(PageView.ContentWidth, PanelHeight);

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ColumnWidth + ColumnGap));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Add(Place(selectors.Cpu, ColumnGap), 0, 0);
        Controls.Add(Place(selectors.Gpu, 0), 1, 0);
    }

    private static Panel Place(Panel selector, int gapRight)
    {
        selector.Dock = DockStyle.Fill;
        selector.Margin = new Padding(0, 0, gapRight, 0);
        return selector;
    }
}
