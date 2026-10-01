using System.ComponentModel;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Performance modes and, in Custom, the CPU and GPU boost levels. There is
/// one profile per power source; the buttons edit the profile for the source
/// the laptop is on now, and that profile is applied automatically at startup
/// and whenever the charger is plugged or unplugged.
/// </summary>
/// <remarks>
/// The EC is the source of truth for what is shown: the display is refreshed
/// from it when the popup opens, so a change made outside the app appears.
/// What is allowed and what gets stored is decided by
/// <see cref="PowerProfileRules"/>; this control only wires that to the UI.
/// </remarks>
internal sealed class PerformanceSection : SectionPanel
{
    private readonly PerformanceService _performanceService;
    private readonly IPowerSource _powerSource;
    private readonly Dictionary<PerformanceMode, Button> _buttons = [];
    private readonly CustomBoostSelectors _customRow = new();
    private readonly Label _temperatureLabel = CreateHeaderValueLabel();
    private readonly ThemedToolTip _toolTip = new();

    // Modes this laptop's firmware was asked for and reported another mode
    // back, so it does not have them. Only known once tried; kept until exit.
    private readonly HashSet<PerformanceMode> _unsupportedModes = [];

    // Keyed by "plugged in".
    private readonly Dictionary<bool, PowerProfile> _profiles;

    private PerformanceState _state = PerformanceState.Unknown;
    private bool? _appliedSource;
    private bool _busy;
    private bool _reapplyRequested;
    private readonly MaxFanMethod _maxFanMethod;
    private bool _autoSwitchProfiles = true;

    public PerformanceSection(
        PerformanceService performanceService,
        IPowerSource powerSource,
        PowerProfile? pluggedInProfile,
        PowerProfile? onBatteryProfile,
        MaxFanMethod maxFanMethod = MaxFanMethod.ControllerFlag)
    {
        _maxFanMethod = maxFanMethod;
        _performanceService = performanceService;
        _powerSource = powerSource;

        _profiles = new Dictionary<bool, PowerProfile>
        {
            [true] = PowerProfileRules.Sanitize(pluggedInProfile ?? new PowerProfile(), pluggedIn: true),
            [false] = PowerProfileRules.Sanitize(onBatteryProfile ?? PowerProfile.DefaultOnBattery, pluggedIn: false)
        };

        // Silent, Balanced and Custom on every model, quietest first
        // (Enum.GetValues would sort by wire byte instead). Gaming is not
        // offered. A mode the firmware turns out not to take is marked as such
        // the first time it is tried.
        PerformanceMode[] modes = [PerformanceMode.Silent, PerformanceMode.Balanced, PerformanceMode.Custom];
        var grid = CreateButtonGrid(modes.Select(mode => mode.ToString()).ToArray(), "PerformanceButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = Enum.Parse<PerformanceMode>((string)button.Tag!);
            _buttons[mode] = button;
            button.Click += async (_, _) =>
            {
                await SelectModeAsync(mode);

                // Custom's levels live in a window of their own. It opens once
                // the EC is in Custom, and clicking Custom again reopens it.
                if (mode == PerformanceMode.Custom && _state.Mode == PerformanceMode.Custom)
                    CustomBoostRequested?.Invoke(this, EventArgs.Empty);
            };

            // The mode names a step bolder than the other buttons' text, and 2pt larger (2pt is 2.67px).
            button.Font = SemiBoldFont(16 + 2 / 0.75F);

            if (button is RoundedButton rounded)
                rounded.Glyph = GlyphFor(mode);
        }

        _customRow.CpuSelected += async (_, level) => await SelectCpuAsync(level);
        _customRow.GpuSelected += async (_, level) => await SelectGpuAsync(level);

        // The title, and the temperatures on the right: empty (and taking no
        // room) until a reading arrives.
        var header = CreateHeaderLayout();
        header.Controls.Add(CreateSectionLabel("Performance Mode"), 0, 0);
        header.Controls.Add(_temperatureLabel, 1, 0);

        // Dock order: the header docks first, and the mode buttons fill whatever is left.
        Controls.Add(grid);
        Controls.Add(header);

        UpdateButtonStates();

        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Raised after the user changes a profile and the EC confirms it.</summary>
    public event EventHandler<PowerProfileChange>? ProfileChanged;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Raised whenever the section shows a new state, so others (the fan buttons) can follow it.</summary>
    public event EventHandler<PerformanceState>? StateChanged;

    /// <summary>Raised when the user clicks Custom and the EC is in it. The host opens the boost window.</summary>
    public event EventHandler? CustomBoostRequested;

    /// <summary>
    /// The CPU and GPU level selectors. The section keeps them up to date
    /// whether or not they are shown; the Custom window borrows them while it
    /// is open and hands them back when it closes.
    /// </summary>
    public CustomBoostSelectors BoostSelectors => _customRow;

    /// <summary>
    /// Whether plugging or unplugging the charger switches to the profile for
    /// that source. Turning it back on catches up straight away if the source
    /// changed while it was off.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoSwitchProfiles
    {
        get => _autoSwitchProfiles;
        set
        {
            if (_autoSwitchProfiles == value)
                return;

            _autoSwitchProfiles = value;

            if (value && IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        }
    }

    /// <summary>
    /// Shows the temperatures in the header, or nothing when there is no
    /// reading. Polled every couple of seconds, so it does no work at all when
    /// the shown text has not changed.
    /// </summary>
    public void ShowTemperatures(TemperatureReading reading)
    {
        var text = TemperatureText.Format(reading.CpuCelsius, reading.GpuCelsius);

        if (text != _temperatureLabel.Text)
            _temperatureLabel.Text = text;
    }

    /// <summary>The icon a mode's button shows, which the shortcut notice shows too.</summary>
    public static Glyph GlyphFor(PerformanceMode mode) => mode switch
    {
        PerformanceMode.Balanced => Glyph.Balanced,
        PerformanceMode.Silent => Glyph.Silent,
        _ => Glyph.Custom
    };

    /// <summary>
    /// Switches mode from a keyboard shortcut, as a click on its button would,
    /// and says how it went for the notice: whether the mode is on, and a
    /// short status ("Active", "Needs to be plugged in"...).
    /// </summary>
    public async Task<(bool Applied, string Status)> SelectFromShortcutAsync(PerformanceMode mode)
    {
        if (_unsupportedModes.Contains(mode))
            return (false, L.T("Not supported on this laptop"));

        if (!PowerProfileRules.IsModeAllowed(mode, IsPluggedIn))
            return (false, L.T("Needs to be plugged in"));

        if (_state.Mode == mode)
            return (true, L.T("Active"));

        if (_busy)
            return (false, L.T("Busy, try again in a moment"));

        await SelectModeAsync(mode);

        if (_state.Mode == mode)
            return (true, L.T("Active"));

        return (false, _unsupportedModes.Contains(mode)
            ? L.T("Not supported on this laptop")
            : L.T("Could not change the performance mode."));
    }

    /// <summary>Applies the profile for the current power source, e.g. at startup.</summary>
    public Task RestoreAsync() => ApplyActiveProfileAsync(atStartup: true);

    /// <summary>
    /// Turns max fan speed on or off. A one-off: it is not stored in a profile,
    /// and any mode change puts the fans back on automatic. Ignored when it is
    /// already in that state, while something else is talking to the EC, or
    /// when turning it on is not available (see PowerProfileRules.CanUseMaxFan).
    /// Turning it off is always allowed.
    /// </summary>
    public Task SetMaxFanAsync(bool enabled)
    {
        if (_busy || enabled == (_state.MaxFan == true) ||
            (enabled && !PowerProfileRules.CanUseMaxFan(_state, IsPluggedIn, _maxFanMethod)))
            return Task.CompletedTask;

        return RunAsync(
            () => _performanceService.SetMaxFanAsync(enabled),
            L.T("Could not change max fan speed."),
            _ => { }); // Nothing to remember: it is not part of a profile.
    }

    /// <summary>
    /// Called as the app exits. Max is a one-off the app is watching over; the
    /// controller keeps the fans flat out after the app has gone (on the Blade
    /// 15 Base 2020 even across restarts of the app), so it is put back on
    /// automatic here. Runs synchronously: the app is about to end.
    /// </summary>
    public void TurnOffMaxFanBeforeExit()
    {
        if (_state.MaxFan != true)
            return;

        try
        {
            _performanceService.SetMaxFan(false);
            AppLog.Info("Max fan speed turned off as RazerHelper exits.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn off max fan speed on exit.", exception);
        }
    }

    /// <summary>Shows the mode and boost levels the EC is actually in.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        var state = PerformanceState.Unknown;

        try
        {
            state = await _performanceService.ReadStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the performance mode.", exception);
        }

        await PostToUiAsync(() =>
        {
            ShowState(state);
            EndBusy();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _toolTip.Dispose();

            // Not a child of the section, so it is not disposed with it.
            _customRow.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool IsPluggedIn => PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);

    private PowerProfile ActiveProfile => _profiles[IsPluggedIn];

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e) =>
        _ = PostToUiAsync(() =>
        {
            UpdateButtonStates();

            // Windows also raises this for battery percentage changes; only a
            // change of source means a different profile.
            if (_autoSwitchProfiles && IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
            else
                TurnOffMaxFanOnBattery();
        });

    /// <param name="atStartup">
    /// Also turn off a Max left on from before the app started (a crash, or a
    /// shutdown with Max on): Max is a one-off, never part of a profile.
    /// </param>
    private Task ApplyActiveProfileAsync(bool atStartup = false)
    {
        if (_busy)
        {
            // Something else is talking to the EC; pick this up when it is done.
            _reapplyRequested = true;
            return Task.CompletedTask;
        }

        var source = IsPluggedIn;
        var profile = _profiles[source];

        return RunAsync(
            async () =>
            {
                var state = await _performanceService.ApplyProfileAsync(profile);

                // Max is a plugged-in setting. A mode change already puts the
                // fans back on automatic; when the profile keeps the same mode
                // (Balanced both plugged in and on battery) it has to be done here.
                return (atStartup || !source) && state.MaxFan == true
                    ? await _performanceService.SetMaxFanAsync(false)
                    : state;
            },
            L.T("Could not apply the power profile."),
            _ => _appliedSource = source);
    }

    // With automatic profile switching off, nothing else runs on unplugging,
    // so Max is turned off on its own.
    private void TurnOffMaxFanOnBattery()
    {
        if (!IsPluggedIn && _state.MaxFan == true)
            _ = SetMaxFanAsync(false);
    }

    private Task SelectModeAsync(PerformanceMode mode) =>
        mode == _state.Mode || !PowerProfileRules.IsModeAllowed(mode, IsPluggedIn) || _unsupportedModes.Contains(mode)
            ? Task.CompletedTask // Already there, not offered on this power source, or not on this laptop.
            : ChangeProfileAsync(profile => profile with { Mode = mode }, L.T("Could not change the performance mode."));

    private Task SelectCpuAsync(CpuBoost level) =>
        !PowerProfileRules.CanChangeBoost(_state, IsPluggedIn) || level == _state.Cpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Cpu = level }, L.T("Could not change the boost level."));

    private Task SelectGpuAsync(GpuBoost level) =>
        !PowerProfileRules.CanChangeBoost(_state, IsPluggedIn) || level == _state.Gpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Gpu = level }, L.T("Could not change the boost level."));

    private Task ChangeProfileAsync(Func<PowerProfile, PowerProfile> edit, string failureMessage)
    {
        if (_busy)
            return Task.CompletedTask;

        var source = IsPluggedIn;
        var edited = edit(_profiles[source]);

        return RunAsync(
            () => _performanceService.ApplyProfileAsync(edited),
            failureMessage,
            state =>
            {
                // Asked for a mode and the firmware settled on another: this
                // laptop does not have it. Remember that rather than offer it again.
                if (edited.Mode is PerformanceMode asked && state.Mode is not null && state.Mode != asked)
                {
                    _unsupportedModes.Add(asked);
                    AppLog.Error($"The EC was asked for {asked} but stayed in {state.Mode}; {asked} is marked as not supported.");
                    StatusChanged?.Invoke(this, new SectionStatus(L.T("This laptop does not support that mode."), IsError: true));
                    UpdateButtonStates();
                }

                var saved = PowerProfileRules.Remember(edited, state);

                _profiles[source] = saved;
                _appliedSource = source;
                ProfileChanged?.Invoke(this, new PowerProfileChange(source, saved));
            });
    }

    // Runs an EC operation off the UI thread, then shows the resulting state.
    // On failure the display goes back to what the EC last confirmed.
    private async Task RunAsync(
        Func<Task<PerformanceState>> operation,
        string failureMessage,
        Action<PerformanceState> onSuccess)
    {
        _busy = true;
        UpdateButtonStates();

        PerformanceState? result = null;
        Exception? failure = null;

        try
        {
            result = await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await PostToUiAsync(() =>
        {
            if (result is not null)
            {
                ShowState(result);
                AppLog.Info($"Performance state is now {result.Mode} (CPU {result.Cpu}, GPU {result.Gpu}, max fan {result.MaxFan?.ToString() ?? "n/a"}).");
                StatusChanged?.Invoke(this, new SectionStatus(L.T("Performance profile applied.")));

                // After the status, so what it finds (a mode the laptop refused) can replace it.
                onSuccess(result);
            }
            else
            {
                AppLog.Error(failureMessage, failure);
                ShowState(_state);
                StatusChanged?.Invoke(this, new SectionStatus(failureMessage, IsError: true));
            }

            EndBusy();
        });
    }

    private void EndBusy()
    {
        _busy = false;
        UpdateButtonStates();

        // A plug or unplug arrived while we were busy.
        if (_reapplyRequested)
        {
            _reapplyRequested = false;

            if (_autoSwitchProfiles && IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        }
    }

    private void ShowState(PerformanceState state)
    {
        _state = state;

        HighlightSelected(
            _buttons.Values,
            // A mode with no button here (Gaming, set by Fn keys or another
            // program) highlights nothing.
            state.Mode is PerformanceMode known ? _buttons.GetValueOrDefault(known) : null);

        // Highlighting resets the text colors, so redo the unavailable look.
        UpdateButtonStates();

        _customRow.ShowBoosts(state.Cpu, state.Gpu);

        StateChanged?.Invoke(this, state);
    }

    private void UpdateButtonStates()
    {
        var pluggedIn = IsPluggedIn;

        foreach (var (mode, button) in _buttons)
        {
            // Truly disabled only while a write is in flight. A mode that is not
            // offered on this power source stays clickable underneath (the click
            // handler refuses it) so hovering it can explain why.
            button.Enabled = !_busy;

            if (_unsupportedModes.Contains(mode))
                SetAvailability(button, false, _toolTip, "Not supported on this laptop");
            else
                SetAvailability(button, PowerProfileRules.IsModeAllowed(mode, pluggedIn), _toolTip, "Needs to be plugged in");
        }

        _customRow.Enabled = !_busy && PowerProfileRules.CanChangeBoost(_state, pluggedIn);
    }
}
