using RazerHelper.Core.Models;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The first section, where the window opens: the performance modes and,
/// under them while the laptop is in Custom, its CPU and GPU levels; then
/// how busy the CPU, GPU and RAM are; the fans; and the battery with its
/// charge limit and a button to Energy.
/// </summary>
internal sealed class PerformancePage : PageView
{
    private readonly SectionStack _stack = new();
    private readonly CustomBoostPanel _custom;
    private readonly UsageGauges _gauges = new();

    // Kept apart from Visible, which reads false whenever the window is hidden.
    private bool _customShown;

    public PerformancePage(PerformanceSection performance, FanSection fans, BatterySection battery)
    {
        _custom = new CustomBoostPanel(performance.BoostSelectors) { Visible = false };

        _stack.AddSection(performance, () => SectionHeaderHeight + S(150) + GlowRoom, wide: true);
        _stack.AddSection(_custom, () => _customShown ? CustomBoostPanel.PanelHeight : 0, joined: true, wide: true);
        _stack.AddSection(_gauges, () => UsageGauges.GaugesHeight, joined: true);
        _stack.AddSection(fans, () => SectionHeaderHeight + RadioOption.PreferredHeight +
            (fans.AreReadingsShown ? FanSection.ReadingsHeight : 0));
        _stack.AddSection(battery, () => BatterySection.ContentHeight);

        // A laptop that does not report fan speeds loses that line.
        fans.ReadingsHidden += (_, _) => _stack.Relayout();

        // The fan poll already reads the CPU temperature; the CPU ring shows it.
        fans.TemperaturesRead += (_, reading) => _gauges.ShowCpuTemperature(reading.CpuCelsius);

        // The levels only mean something in Custom; the charger switching
        // profiles, or the Fn keys, can leave it while the window is open.
        performance.StateChanged += (_, state) => ShowCustom(state.Mode == PerformanceMode.Custom);

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
