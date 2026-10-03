using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The first section, where the window opens: the performance modes (with
/// the temperatures), the fans, and the battery with its charge limit and a
/// button to Battery and power.
/// </summary>
internal sealed class PerformancePage : PageView
{
    private readonly SectionStack _stack = new();

    public PerformancePage(PerformanceSection performance, FanSection fans, BatterySection battery)
        : base("Performance")
    {
        _stack.AddSection(performance, () => SectionHeaderHeight + S(136));
        _stack.AddSection(fans, () => SectionHeaderHeight + RadioOption.PreferredHeight +
            (fans.AreReadingsShown ? FanSection.ReadingsHeight : 0));
        _stack.AddSection(battery, () => BatterySection.ContentHeight);

        // A laptop that does not report fan speeds loses that line.
        fans.ReadingsHidden += (_, _) => _stack.Relayout();

        Add(_stack);
    }
}
