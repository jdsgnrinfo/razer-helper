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
/// switch that changes between them with the charger; and the option to
/// switch Windows' power plan while the laptop is left alone. A choice shown
/// only with its switch on stays hidden while it is off. Every change
/// applies and is saved at once.
/// </summary>
internal sealed class PowerPage : PageView
{
    /// <summary>The idle times offered, in minutes.</summary>
    public static readonly int[] MinuteChoices = [1, 5, 10, 15, 20, 25, 30];

    private static readonly PerformanceMode[] Modes = [PerformanceMode.Silent, PerformanceMode.Balanced, PerformanceMode.Custom];

    private readonly BatteryDetailsView _battery;
    private readonly PerformanceSection _performance;
    private readonly ThemedToolTip _toolTip = new();
    private readonly ToggleSwitch _profilesSwitch;
    private readonly FlowLayoutPanel _profiles;
    private readonly Dictionary<bool, Dictionary<PerformanceMode, Button>> _profileButtons = [];
    private readonly ToggleSwitch _idleSwitch;
    private readonly FlowLayoutPanel _idleOptions;
    private readonly DropdownButton _minutes;
    private readonly DropdownButton _plans;
    private IReadOnlyList<PowerPlan> _planList;

    public PowerPage(
        Func<BatteryDetails?> readBattery,
        PerformanceSection performance,
        bool autoSwitchProfiles,
        (bool Enabled, int Minutes, Guid? Plan) idle,
        IReadOnlyList<PowerPlan> plans)
    {
        _performance = performance;
        _planList = plans;

        _battery = new BatteryDetailsView(readBattery);
        Add(_battery);

        // Read once now, so the window is sized for the cards before it first opens.
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

        // Idle: the switch, and under it, only while it is on, how long and which plan.
        var idleCard = CreateSwitchCard("Change the power plan when idle", "Your plan comes back as soon as you use the laptop.", out _idleSwitch);
        Add(CreateDivider());
        Add(idleCard);

        _minutes = new DropdownButton([.. MinuteChoices.Select(choice => L.F("{0} min", choice))])
        {
            Anchor = AnchorStyles.Right,
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(116, 38))
        };

        _plans = CreatePlanList(plans);

        _idleOptions = CreateGroup();
        _idleOptions.Controls.Add(CreateCard("Idle for", "Without touching the keyboard or the mouse.", _minutes));
        _idleOptions.Controls.Add(CreatePlanCard());
        _idleOptions.Controls.Add(new InfoNote(L.T("A video or a game that keeps the screen on counts as using the laptop."), ContentWidth)
        {
            Margin = new Padding(0, S(4), 0, 0)
        });
        Add(_idleOptions);

        _idleSwitch.Checked = idle.Enabled;
        _idleOptions.Visible = idle.Enabled;
        _minutes.Select(Array.IndexOf(MinuteChoices, idle.Minutes));
        _plans.Select(idle.Plan is { } chosen ? IndexOfPlan(chosen) : -1);

        _idleSwitch.CheckedChanged += (_, _) =>
        {
            _idleOptions.Visible = _idleSwitch.Checked;
            RaiseIdleChanged();
        };
        _minutes.SelectionChanged += (_, _) => RaiseIdleChanged();
        _plans.SelectionChanged += (_, _) => RaiseIdleChanged();
    }

    /// <summary>Raised by the profiles' switch.</summary>
    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    /// <summary>Raised with every idle change: on or off, the minutes, and the plan (null while none is picked).</summary>
    public event EventHandler<(bool Enabled, int Minutes, Guid? Plan)>? IdleChanged;

    /// <summary>Asked each time the page is shown, so a plan added meanwhile is offered.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<IReadOnlyList<PowerPlan>>? ListPlans { get; set; }

    public override void OnPageShown()
    {
        ShowProfiles();
        RefreshPlans();
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

    // What follows a switch, pressed close under it: no line between them,
    // so they read as one option.
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

    private Control CreatePlanCard() => CreateCard("Power plan", "Applied once that time has passed.", _plans);

    private static DropdownButton CreatePlanList(IReadOnlyList<PowerPlan> plans) => new([.. plans.Select(each => each.Name)])
    {
        Anchor = AnchorStyles.Right,
        Font = SemiBoldTitleFont(16),
        Size = S(new Size(200, 38))
    };

    // The list is rebuilt only when the plans themselves changed.
    private void RefreshPlans()
    {
        if (ListPlans?.Invoke() is not { } plans || plans.Select(plan => (plan.Id, plan.Name)).SequenceEqual(_planList.Select(plan => (plan.Id, plan.Name))))
            return;

        Guid? chosen = _plans.SelectedIndex >= 0 ? _planList[_plans.SelectedIndex].Id : null;
        _planList = plans;
        _plans.Replace([.. plans.Select(each => each.Name)]);
        _plans.Select(chosen is { } id ? IndexOfPlan(id) : -1);
    }

    private int IndexOfPlan(Guid plan)
    {
        for (var index = 0; index < _planList.Count; index++)
        {
            if (_planList[index].Id == plan)
                return index;
        }

        return -1;
    }

    private void RaiseIdleChanged()
    {
        var minutes = _minutes.SelectedIndex >= 0 ? MinuteChoices[_minutes.SelectedIndex] : MinuteChoices[1];
        Guid? plan = _plans.SelectedIndex >= 0 ? _planList[_plans.SelectedIndex].Id : null;
        IdleChanged?.Invoke(this, (_idleSwitch.Checked, minutes, plan));
    }
}
