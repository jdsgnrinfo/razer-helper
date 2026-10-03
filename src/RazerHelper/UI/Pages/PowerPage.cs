using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The battery's figures; the profiles for each power source, under the
/// switch that changes between them with the charger, and hidden while it
/// is off. Every change applies and is saved at once.
/// </summary>
internal sealed class PowerPage : PageView
{
    private static readonly PerformanceMode[] Modes = [PerformanceMode.Silent, PerformanceMode.Balanced, PerformanceMode.Custom];

    private readonly BatteryDetailsView _battery;
    private readonly PerformanceSection _performance;
    private readonly ThemedToolTip _toolTip = new();
    private readonly ToggleSwitch _profilesSwitch;
    private readonly FlowLayoutPanel _profiles;
    private readonly Dictionary<bool, Dictionary<PerformanceMode, Button>> _profileButtons = [];

    public PowerPage(
        Func<BatteryDetails?> readBattery,
        PerformanceSection performance,
        bool autoSwitchProfiles)
    {
        _performance = performance;

        _battery = new BatteryDetailsView(readBattery);
        Add(_battery);

        // Read once now, so the cards are there the moment the page first opens.
        _battery.Refresh();

        // The profiles: the switch, and under it the mode for each source.
        var profilesCard = CreateSwitchCard("Power source profiles", "When you plug in or unplug the charger, switches to the profile chosen for each.", out _profilesSwitch);
        var divider = CreateDivider();
        divider.Margin = new Padding(0, S(12), 0, 0);
        Add(divider);
        Add(profilesCard);

        _profiles = CreateGroup();
        _profiles.Controls.Add(CreateProfileRow("Plugged in", pluggedIn: true));
        _profiles.Controls.Add(CreateProfileRow("On battery", pluggedIn: false));
        Add(_profiles);

        _profilesSwitch.Checked = autoSwitchProfiles;
        _profiles.Visible = autoSwitchProfiles;
        _profilesSwitch.CheckedChanged += (_, _) =>
        {
            _profiles.Visible = _profilesSwitch.Checked;
            AutoSwitchProfilesChanged?.Invoke(this, _profilesSwitch.Checked);
        };

        _performance.ProfileChanged += Performance_ProfileChanged;
        _performance.StateChanged += Performance_StateChanged;
        ShowProfiles();
    }

    /// <summary>Raised by the profiles' switch.</summary>
    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public override void OnPageShown()
    {
        ShowProfiles();
        _battery.Start();
    }

    public override void OnPageHidden() => _battery.Stop();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _performance.ProfileChanged -= Performance_ProfileChanged;
            _performance.StateChanged -= Performance_StateChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Performance_ProfileChanged(object? sender, PowerProfileChange change) => ShowProfiles();

    private void Performance_StateChanged(object? sender, PerformanceState state) => ShowProfiles();

    // What follows the switch, close under it with no line between, so they
    // read as one option.
    private static FlowLayoutPanel CreateGroup() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = BackgroundColor,
        FlowDirection = FlowDirection.TopDown,
        Margin = Padding.Empty,
        Padding = Padding.Empty,
        WrapContents = false
    };

    // "Plugged in" on the left and the three modes on the right.
    private Control CreateProfileRow(string text, bool pluggedIn)
    {
        var grid = CreateButtonGrid(Modes.Select(mode => mode.ToString()).ToArray(), pluggedIn ? "PluggedInProfileButton" : "OnBatteryProfileButton");
        grid.Dock = DockStyle.None;
        grid.Anchor = AnchorStyles.Right;
        grid.Size = S(new Size(360, 38));

        var buttons = new Dictionary<PerformanceMode, Button>();

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = Enum.Parse<PerformanceMode>((string)button.Tag!);
            button.Font = SemiBoldTitleFont(15);
            button.Click += async (_, _) =>
            {
                if (_performance.IsModeOffered(mode, pluggedIn))
                    await _performance.SetModeForAsync(pluggedIn, mode);
            };
            buttons[mode] = button;
        }

        _profileButtons[pluggedIn] = buttons;

        var row = CreateCard(text, string.Empty, grid);
        row.Padding = new Padding(0, S(4), 0, S(8));
        return row;
    }

    private void ShowProfiles()
    {
        foreach (var (pluggedIn, buttons) in _profileButtons)
        {
            var mode = _performance.ModeFor(pluggedIn);
            HighlightSelected(buttons.Values, mode is { } chosen ? buttons.GetValueOrDefault(chosen) : null);

            foreach (var (each, button) in buttons)
            {
                var reason = PowerProfileRules.IsModeAllowed(each, pluggedIn) ? "Not supported on this laptop" : "Needs to be plugged in";
                SetAvailability(button, _performance.IsModeOffered(each, pluggedIn), _toolTip, reason);
            }
        }
    }
}
