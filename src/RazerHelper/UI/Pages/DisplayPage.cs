using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The display's refresh rate, the keyboard and logo lighting, and the
/// option that turns the keyboard off with the screen.
/// </summary>
internal sealed class DisplayPage : PageView
{
    private readonly ToggleSwitch _keyboardOffWithScreen;

    public DisplayPage(DisplaySection display, LightingSection lighting, bool keyboardOffWithScreen)
    {
        var stack = new SectionStack();
        stack.AddSection(display, () => SectionHeaderHeight + S(68));
        stack.AddSection(lighting, () => LightingSection.ContentHeight);
        Add(stack);

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
