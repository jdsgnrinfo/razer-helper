using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The Custom performance mode's CPU and GPU boost selectors, each a settings
/// row: the chip's icon and name, and at the right a list of its levels.
/// Two separate rows, so the Custom panel can place them.
/// </summary>
internal sealed class CustomBoostSelectors : IDisposable
{
    private static readonly CpuBoost[] CpuLevels = Enum.GetValues<CpuBoost>();
    private static readonly GpuBoost[] GpuLevels = Enum.GetValues<GpuBoost>();

    private readonly DropdownButton _cpuList;
    private readonly DropdownButton _gpuList;

    public CustomBoostSelectors()
    {
        (Cpu, _cpuList) = CreateSelector("CPU", NavIcon.Cpu, CpuLevels, level => CpuSelected?.Invoke(this, level));
        (Gpu, _gpuList) = CreateSelector("GPU", NavIcon.Gpu, GpuLevels, level => GpuSelected?.Invoke(this, level));
        Gpu.Divided = true;
    }

    /// <summary>The CPU's row, to place in the Custom panel.</summary>
    public SettingRow Cpu { get; }

    /// <summary>The GPU's row, to place in the Custom panel.</summary>
    public SettingRow Gpu { get; }

    public event EventHandler<CpuBoost>? CpuSelected;

    public event EventHandler<GpuBoost>? GpuSelected;

    /// <summary>Whether the lists can be used (not while a change is being sent, nor where boosts cannot change).</summary>
    public bool Enabled
    {
        set
        {
            Cpu.Enabled = value;
            Gpu.Enabled = value;
        }
    }

    /// <summary>Shows the given levels in the lists; null shows none.</summary>
    public void ShowBoosts(CpuBoost? cpu, GpuBoost? gpu)
    {
        _cpuList.Select(cpu is { } c ? Array.IndexOf(CpuLevels, c) : -1);
        _gpuList.Select(gpu is { } g ? Array.IndexOf(GpuLevels, g) : -1);
    }

    public void Dispose()
    {
        Cpu.Dispose();
        Gpu.Dispose();
    }

    private static (SettingRow Row, DropdownButton List) CreateSelector<TLevel>(string title, NavIcon icon, TLevel[] levels, Action<TLevel> onSelected)
        where TLevel : struct, Enum
    {
        var list = new DropdownButton([.. levels.Select(level => LevelName(level.ToString()))])
        {
            AccessibleName = title,
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(190, 38))
        };

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
                onSelected(levels[list.SelectedIndex]);
        };

        var row = new SettingRow(title, icon: icon);
        row.Add(list, outset: S(4));
        return (row, list);
    }

    private static string LevelName(string level) => level switch
    {
        "Low" => L.T("Low"),
        "Medium" => L.T("Medium"),
        "High" => L.T("High"),
        _ => L.T("Boost")
    };
}
