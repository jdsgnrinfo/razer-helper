using RazerHelper.Core.Models;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// CPU and GPU boost selectors for the Custom performance mode, the GPU's
/// under the CPU's, shown in the Custom window. Stacked rather than side by
/// side so every label ("Medium" included) fits.
/// </summary>
internal sealed class CustomBoostRow : TableLayoutPanel
{
    // Each selector: its title and a row of buttons.
    private static int SelectorHeight => S(66);

    public static int RowHeight => 2 * SelectorHeight;

    private readonly Dictionary<CpuBoost, Button> _cpuButtons = [];
    private readonly Dictionary<GpuBoost, Button> _gpuButtons = [];

    public CustomBoostRow()
    {
        BackColor = CardColor;
        ColumnCount = 1;
        Height = RowHeight;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 2;

        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        RowStyles.Add(new RowStyle(SizeType.Absolute, SelectorHeight));
        RowStyles.Add(new RowStyle(SizeType.Absolute, SelectorHeight));

        Controls.Add(CreateSelector("CPU", Glyph.Cpu, Enum.GetValues<CpuBoost>(), _cpuButtons, level => CpuSelected?.Invoke(this, level)), 0, 0);
        Controls.Add(CreateSelector("GPU", Glyph.Gpu, Enum.GetValues<GpuBoost>(), _gpuButtons, level => GpuSelected?.Invoke(this, level)), 0, 1);
    }

    public event EventHandler<CpuBoost>? CpuSelected;

    public event EventHandler<GpuBoost>? GpuSelected;

    /// <summary>Highlights the given levels; null clears a selector.</summary>
    public void ShowBoosts(CpuBoost? cpu, GpuBoost? gpu)
    {
        HighlightSelected(_cpuButtons.Values, cpu is CpuBoost c ? _cpuButtons[c] : null);
        HighlightSelected(_gpuButtons.Values, gpu is GpuBoost g ? _gpuButtons[g] : null);
    }

    private static Panel CreateSelector<TLevel>(
        string title,
        Glyph glyph,
        TLevel[] levels,
        Dictionary<TLevel, Button> buttons,
        Action<TLevel> onSelected)
        where TLevel : struct, Enum
    {
        var panel = new Panel
        {
            BackColor = CardColor,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var grid = CreateButtonGrid(levels.Select(level => level.ToString()).ToArray(), $"{title}BoostButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var level = Enum.Parse<TLevel>((string)button.Tag!);
            buttons[level] = button;
            button.Click += (_, _) => onSelected(level);
        }

        // Dock order: the label docks first, the buttons fill what is left.
        panel.Controls.Add(grid);
        // The title with its icon, inset 4px to line up with the buttons.
        var label = CreateSectionLabel(title, glyph, inset: S(4));
        label.AutoSize = false;
        label.Dock = DockStyle.Top;
        label.Font = GetDesignFont("Segoe UI", 9.5F, FontStyle.Bold);
        label.Height = S(22);
        panel.Controls.Add(label);

        return panel;
    }
}
