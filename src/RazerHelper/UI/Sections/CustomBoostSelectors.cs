using RazerHelper.Core.Models;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The Custom performance mode's CPU and GPU boost selectors, each its title
/// over a row of level buttons. Two separate panels, so the Custom panel can
/// put each chip's figures under its own selector.
/// </summary>
internal sealed class CustomBoostSelectors : IDisposable
{
    /// <summary>One selector: its title row and its buttons, with their glow room around them.</summary>
    public static int SelectorHeight => SectionHeaderHeight + S(44) + GlowRoom;

    private readonly Dictionary<CpuBoost, Button> _cpuButtons = [];
    private readonly Dictionary<GpuBoost, Button> _gpuButtons = [];

    public CustomBoostSelectors()
    {
        Cpu = CreateSelector("CPU", Glyph.Cpu, Enum.GetValues<CpuBoost>(), _cpuButtons, level => CpuSelected?.Invoke(this, level));
        Gpu = CreateSelector("GPU", Glyph.Gpu, Enum.GetValues<GpuBoost>(), _gpuButtons, level => GpuSelected?.Invoke(this, level));
    }

    /// <summary>The CPU's selector, to place in the Custom panel.</summary>
    public Panel Cpu { get; }

    /// <summary>The GPU's selector, to place in the Custom panel.</summary>
    public Panel Gpu { get; }

    public event EventHandler<CpuBoost>? CpuSelected;

    public event EventHandler<GpuBoost>? GpuSelected;

    /// <summary>Whether the buttons can be used (not while a change is being sent, nor where boosts cannot change).</summary>
    public bool Enabled
    {
        set
        {
            Cpu.Enabled = value;
            Gpu.Enabled = value;
        }
    }

    /// <summary>Highlights the given levels; null clears a selector.</summary>
    public void ShowBoosts(CpuBoost? cpu, GpuBoost? gpu)
    {
        HighlightSelected(_cpuButtons.Values, cpu is CpuBoost c ? _cpuButtons[c] : null);
        HighlightSelected(_gpuButtons.Values, gpu is GpuBoost g ? _gpuButtons[g] : null);
    }

    public void Dispose()
    {
        Cpu.Dispose();
        Gpu.Dispose();
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
            BackColor = BackgroundColor,
            Height = SelectorHeight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var grid = CreateButtonGrid(levels.Select(level => level.ToString()).ToArray(), $"{title}BoostButton", glowRoom: GlowRoom);
        grid.BackColor = BackgroundColor;

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var level = Enum.Parse<TLevel>((string)button.Tag!);
            buttons[level] = button;

            // The chips' font, as Energy's: four levels to a half-width row leave little room.
            button.Font = SemiBoldTitleFont(15);
            button.Click += (_, _) => onSelected(level);
        }

        // Dock order: the title docks first, the buttons fill what is left.
        panel.Controls.Add(grid);
        // The title lines up with the buttons, inside their glow room.
        var header = CreateSectionHeader(title, string.Empty, glyph);
        header.Height = SectionHeaderHeight - GlowRoom;
        header.Padding = new Padding(GlowRoom, 0, GlowRoom, S(12) - GlowRoom);
        panel.Controls.Add(header);
        return panel;
    }
}
