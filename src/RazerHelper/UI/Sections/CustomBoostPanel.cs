using RazerHelper.UI.Pages;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Custom mode's levels, opened under the mode buttons while the laptop is in
/// Custom: the CPU's row, then the GPU's. Each click applies at once. The
/// selectors belong to the Performance section, which keeps them up to date.
/// It is as wide as a page's wide parts, so the levels' glow has room (see
/// PageView.AddWide).
/// </summary>
internal sealed class CustomBoostPanel : Panel
{
    private readonly CustomBoostSelectors _selectors;

    public CustomBoostPanel(CustomBoostSelectors selectors)
    {
        _selectors = selectors;

        BackColor = BackgroundColor;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        Controls.Add(selectors.Cpu);
        Controls.Add(selectors.Gpu);
        selectors.Gpu.Top = selectors.Cpu.Bottom;
        Size = new Size(PageView.WideWidth, PanelHeight);
    }

    /// <summary>How tall the panel is: the two rows.</summary>
    public int PanelHeight => _selectors.Cpu.Height + _selectors.Gpu.Height;
}
