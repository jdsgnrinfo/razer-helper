using RazerHelper.Core.Hardware;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;

namespace RazerHelper.UI.Pages;

/// <summary>The panel's figures, then the display's refresh rate and its color profile.</summary>
internal sealed class DisplayPage : PageView
{
    private readonly DisplayInfoView _info;

    public DisplayPage(DisplaySection display, ColorProfileSection colorProfile, Func<DisplayPanel?> readPanel, Func<IReadOnlyList<int>> refreshRates)
        : base("Display")
    {
        _info = new DisplayInfoView(readPanel, refreshRates) { Margin = new Padding(0, 0, 0, UiTheme.S(14)) };
        Add(_info);

        var stack = new SectionStack();
        stack.AddSection(display, () => SectionHeaderHeight + UiTheme.S(96) + GlowRoom, wide: true);
        stack.AddSection(colorProfile, () => ColorProfileSection.SectionHeight, wide: true);
        AddWide(stack);
    }

    public override void OnPageShown() => _info.Start();

    public override void OnPageHidden() => _info.Stop();
}
