using RazerHelper.Core.Models;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The first section, where the window opens: the performance modes and,
/// right under them while the laptop is in Custom, its CPU and GPU levels;
/// then how busy the CPU, GPU and RAM are; then the battery's charge limit
/// and Max RPM, one row each.
/// </summary>
internal sealed class PerformancePage : PageView
{
    private readonly SectionStack _stack = new();
    private readonly CustomBoostPanel _custom;
    private readonly UsageGauges _gauges = new();

    // Kept apart from Visible, which reads false whenever the window is hidden.
    private bool _customShown;

    public PerformancePage(PerformanceSection performance, FanSection fans, BatterySection battery)
        : base("Performance")
    {
        _custom = new CustomBoostPanel(performance.BoostSelectors) { Visible = false };

        var monitor = new Panel { BackColor = BackgroundColor, Dock = DockStyle.Fill, Margin = Padding.Empty };
        monitor.Controls.Add(CreateSectionLabel("System monitor", NavIcon.Monitor));

        // The modes' glow room is part of their height, so the gaps under them take it back.
        _stack.AddSection(performance, () => PerformanceSection.ButtonHeight + 2 * GlowRoom, wide: true);
        _stack.AddSection(_custom, () => _customShown ? _custom.PanelHeight : 0, wide: true, gapAbove: () => S(16) - GlowRoom);
        _stack.AddSection(monitor, () => S(20), gapAbove: () => S(26) - (_customShown ? 0 : GlowRoom));
        _stack.AddSection(_gauges, () => UsageGauges.GaugesHeight, gapAbove: () => S(12));
        _stack.AddSection(battery, () => battery.RowHeight, wide: true, gapAbove: () => S(22));
        _stack.AddSection(fans, () => fans.RowHeight, wide: true, gapAbove: () => 0);

        // The fan poll already reads the CPU temperature; the CPU ring shows it.
        fans.TemperaturesRead += (_, reading) => _gauges.ShowCpuTemperature(reading.CpuCelsius);

        // The levels only mean something in Custom; the charger switching
        // profiles, or the Fn keys, can leave it while the window is open.
        performance.StateChanged += (_, state) => ShowCustom(state.Mode == PerformanceMode.Custom);

        TuckUnderTitle();
        AddWide(_stack);
    }

    public override void OnPageShown() => _gauges.Start();

    public override void OnPageHidden() => _gauges.Stop();

    private void ShowCustom(bool shown)
    {
        if (_customShown == shown)
            return;

        _customShown = shown;
        _custom.Visible = shown;
        _stack.Relayout();
    }
}
