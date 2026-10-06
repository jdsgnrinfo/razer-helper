using RazerHelper.Core.Models;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The Custom performance mode's CPU and GPU boost selectors, each a settings
/// row: the chip's icon and name, and its levels at the right, as wide as
/// their names. Two separate rows, so the Custom panel can place them.
/// </summary>
internal sealed class CustomBoostSelectors : IDisposable
{
    private readonly Dictionary<CpuBoost, Button> _cpuButtons = [];
    private readonly Dictionary<GpuBoost, Button> _gpuButtons = [];

    public CustomBoostSelectors()
    {
        Cpu = CreateSelector("CPU", NavIcon.Cpu, Enum.GetValues<CpuBoost>(), _cpuButtons, level => CpuSelected?.Invoke(this, level));
        Gpu = CreateSelector("GPU", NavIcon.Gpu, Enum.GetValues<GpuBoost>(), _gpuButtons, level => GpuSelected?.Invoke(this, level));
        Gpu.Divided = true;
    }

    /// <summary>The CPU's row, to place in the Custom panel.</summary>
    public SettingRow Cpu { get; }

    /// <summary>The GPU's row, to place in the Custom panel.</summary>
    public SettingRow Gpu { get; }

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

    private static SettingRow CreateSelector<TLevel>(
        string title,
        NavIcon icon,
        TLevel[] levels,
        Dictionary<TLevel, Button> buttons,
        Action<TLevel> onSelected)
        where TLevel : struct, Enum
    {
        var strip = CreateChoiceStrip(levels.Select(level => level.ToString()).ToArray(), $"{title}BoostButton", SemiBoldTitleFont(15));

        foreach (var button in strip.Controls.OfType<Button>())
        {
            var level = Enum.Parse<TLevel>((string)button.Tag!);
            buttons[level] = button;
            button.Click += (_, _) => onSelected(level);
        }

        return new SettingRow(title, icon: icon).Add(strip, GlowRoom);
    }
}
