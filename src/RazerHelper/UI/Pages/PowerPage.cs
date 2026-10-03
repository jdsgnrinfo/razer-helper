using System.ComponentModel;
using RazerHelper.Core.Diagnostics;
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
/// is off; and Windows' power plan, with a button to install ours while it
/// is not there. Every change applies and is saved at once.
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
    private readonly IPowerPlans _plans;
    private readonly DropdownButton _planList;
    private readonly Button _installButton;
    private readonly Label _planHint;
    private IReadOnlyList<PowerPlan> _planChoices = [];
    private bool _installing;

    public PowerPage(
        Func<BatteryDetails?> readBattery,
        PerformanceSection performance,
        bool autoSwitchProfiles,
        IPowerPlans plans)
    {
        _performance = performance;
        _plans = plans;

        _battery = new BatteryDetailsView(readBattery);
        Add(_battery);

        // Read once now, so the cards are there the moment the page first opens.
        _battery.Refresh();

        // The profiles: the switch, and under it the mode for each source.
        var profilesCard = CreateSwitchCard("Power source profiles", "When you plug in or unplug the charger, switches to the profile chosen for each.", out _profilesSwitch);
        var divider = CreateDivider();
        divider.Margin = new Padding(0, S(24), 0, S(12));
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

        // Windows' plan: the list of the plans on this PC, and our own plan
        // to install while it is not.
        _planList = new DropdownButton([])
        {
            Font = SemiBoldTitleFont(16),
            Margin = Padding.Empty,
            Size = S(new Size(190, 38))
        };
        _planList.SelectionChanged += (_, _) => ActivateChosenPlan();

        _installButton = CreateSmallButton("Install Razer Blade plan");
        _installButton.BackColor = RazerGreen;
        _installButton.ForeColor = OnGreenTextColor;
        _installButton.Height = S(38);
        _installButton.Width += S(16);
        _installButton.Margin = new Padding(0, 0, S(8), 0);
        _installButton.Click += async (_, _) => await InstallPlanAsync();

        // Right to left, so the list keeps the right edge and the button sits before it.
        var controls = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = new Size(_planList.Width + _installButton.Width + S(8), _planList.Height),
            WrapContents = false
        };
        controls.Controls.Add(_planList);
        controls.Controls.Add(_installButton);

        var planCard = CreateCard("Power plan", "The one Windows uses.", controls);
        _planHint = HintOf(planCard);
        var planDivider = CreateDivider();
        planDivider.Margin = new Padding(0, S(12), 0, 0);
        Add(planDivider);
        Add(planCard);

        ShowPlans();
    }

    /// <summary>Raised by the profiles' switch.</summary>
    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public override void OnPageShown()
    {
        ShowProfiles();
        ShowPlans();
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

    // The plans as Windows has them now: the list (rebuilt only when it
    // changed), the active one picked, and the install button only while
    // our plan is not there.
    private void ShowPlans()
    {
        IReadOnlyList<PowerPlan> plans;
        Guid active;

        try
        {
            plans = _plans.List();
            active = _plans.Active();
        }
        catch (Win32Exception exception)
        {
            AppLog.Error("Could not read the power plans.", exception);
            return;
        }

        if (!plans.SequenceEqual(_planChoices))
        {
            _planChoices = plans;
            _planList.Replace([.. plans.Select(plan => plan.Name)]);
        }

        _planList.Select(IndexOf(active));
        _installButton.Visible = !_installing && plans.All(plan => plan.Id != RazerBladePlan.Id);
    }

    private int IndexOf(Guid plan)
    {
        for (var index = 0; index < _planChoices.Count; index++)
        {
            if (_planChoices[index].Id == plan)
                return index;
        }

        return -1;
    }

    private void ActivateChosenPlan()
    {
        if (_planList.SelectedIndex < 0)
            return;

        var plan = _planChoices[_planList.SelectedIndex];

        try
        {
            _plans.Activate(plan.Id);
            AppLog.Info($"Power plan changed to {plan.Name}.");
            _planHint.Text = L.T("The one Windows uses.");
        }
        catch (Win32Exception exception)
        {
            AppLog.Error("Could not change the power plan.", exception);
            _planHint.Text = L.T("Windows did not change the plan.");
            ShowPlans();
        }
    }

    // Adds our plan (or brings it up to date) off the window's thread, as
    // powercfg takes a moment, then makes it the one in use.
    private async Task InstallPlanAsync()
    {
        if (_installing)
            return;

        _installing = true;
        _installButton.Enabled = false;
        _planHint.Text = L.T("Installing...");

        try
        {
            var spanish = L.Current == AppLanguage.Spanish;
            var refused = await Task.Run(() => RazerBladePlan.Install(_plans, spanish));

            AppLog.Info(refused.Count == 0
                ? "Razer Blade power plan installed and made active."
                : $"Razer Blade power plan installed; this PC refused {string.Join(", ", refused)}.");
            _planHint.Text = L.T("Razer Blade plan installed and in use.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not install the Razer Blade power plan.", exception);
            _planHint.Text = L.T("Could not install the Razer Blade plan.");
        }
        finally
        {
            _installing = false;
            _installButton.Enabled = true;

            if (!IsDisposed)
                ShowPlans();
        }
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
