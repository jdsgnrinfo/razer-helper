using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;

namespace RazerHelper.UI.Pages;

/// <summary>The display's refresh rate and its color profile.</summary>
internal sealed class DisplayPage : PageView
{
    public DisplayPage(DisplaySection display, ColorProfileSection colorProfile)
        : base("Display")
    {
        var stack = new SectionStack();
        stack.AddSection(display, () => SectionHeaderHeight + UiTheme.S(68) + GlowRoom, wide: true);
        stack.AddSection(colorProfile, () => ColorProfileSection.SectionHeight, wide: true);
        AddWide(stack);
    }
}
