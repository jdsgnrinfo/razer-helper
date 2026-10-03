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
        AddWide(_profiles);

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
            Size = S(new Size(190, 38))
        };
        _planList.SelectionChanged += (_, _) => ActivateChosenPlan();

        _installButton = CreateSmallButton("Install Razer Blade plan");
        _installButton.BackColor = RazerGreen;
        _installButton.ForeColor = OnGreenTextColor;
        ((RoundedButton)_installButton).GlowRoom = ButtonGlowInset;
        _installButton.Height = S(38) + 2 * ButtonGlowInset;
        _installButton.Width += S(16) + 2 * ButtonGlowInset;
        _installButton.Click += async (_, _) => await InstallPlanAsync();

        // The list at the right edge of the content and the button 8px before
        // it, placed by hand: the panel reaches the glow room past the content
        // at the right, and keeps the rest of the button's glow room around it
        // (it paints the glow; see Glow).
        var edge = GlowRoom - ButtonGlowInset;
        var controls = new Panel
        {
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = new Size(edge + _installButton.Width + S(8) - ButtonGlowInset + _planList.Width + GlowRoom, _installButton.Height + 2 * edge)
        };
        _installButton.Location = new Point(edge, edge);
        _planList.Location = new Point(controls.Width - GlowRoom - _planList.Width, (controls.Height - _planList.Height) / 2);
        controls.Controls.Add(_planList);
        controls.Controls.Add(_installButton);
        Glow.Attach(controls);

        var planCard = Widen(CreateCard("Power plan", "The one Windows uses.", controls), controls);
        _planHint = HintOf(planCard);
        var planDivider = CreateDivider();
        planDivider.Margin = new Padding(0, S(12), 0, 0);
        Add(planDivider);
        AddWide(planCard);

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
    // A setting's row made as wide as the parts with glowing buttons (see
    // AddWide): the text keeps its place, and the control reaches into the
    // glow room at the right, so its buttons still end at the content's edge.
    private static TableLayoutPanel Widen(TableLayoutPanel card, Control control)
    {
        card.Padding = new Padding(GlowRoom, card.Padding.Top, 0, card.Padding.Bottom);
        card.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, ContentWidth + GlowRoom - control.Width);
        return card;
    }

    private Control CreateProfileRow(string text, bool pluggedIn)
    {
        var grid = CreateButtonGrid(Modes.Select(mode => mode.ToString()).ToArray(), pluggedIn ? "PluggedInProfileButton" : "OnBatteryProfileButton", glowRoom: GlowRoom);
        grid.Dock = DockStyle.None;
        grid.Anchor = AnchorStyles.Right;
        grid.Size = new Size(S(360) + 2 * GlowRoom, S(38) + 2 * GlowRoom);

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

        // The glow room above and below takes from the row's own padding.
        var row = Widen(CreateCard(text, string.Empty, grid), grid);
        row.Padding = new Padding(GlowRoom, 0, 0, Math.Max(0, S(8) - GlowRoom));
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
