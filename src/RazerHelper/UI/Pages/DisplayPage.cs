using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The display's refresh rate and color profile, the keyboard and logo lighting, and the
/// option that turns the keyboard off with the screen.
/// </summary>
internal sealed class DisplayPage : PageView
{
    private readonly ToggleSwitch _keyboardOffWithScreen;

    public DisplayPage(DisplaySection display, ColorProfileSection colorProfile, LightingSection lighting, bool keyboardOffWithScreen)
    {
        var stack = new SectionStack();
        stack.AddSection(display, () => SectionHeaderHeight + S(68) + GlowRoom, wide: true);
        stack.AddSection(colorProfile, () => ColorProfileSection.SectionHeight, wide: true);
        stack.AddSection(lighting, () => LightingSection.ContentHeight);
        AddWide(stack);

        var option = CreateSwitchCard("Keyboard off with the screen", "Turns the lighting off and back on with the screen.", out _keyboardOffWithScreen);
        var divider = CreateDivider();
        divider.Margin = new Padding(0, S(26), 0, 0);
        Add(divider);
        Add(option);

        _keyboardOffWithScreen.Checked = keyboardOffWithScreen;
        _keyboardOffWithScreen.CheckedChanged += (_, _) => KeyboardOffWithScreenChanged?.Invoke(this, _keyboardOffWithScreen.Checked);
    }

    public event EventHandler<bool>? KeyboardOffWithScreenChanged;
}
