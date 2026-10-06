using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Max RPM, as one settings row: its switch, and under its name the CPU and
/// GPU fans' speeds as they are now. Every poll it also reads the CPU and GPU
/// temperatures and announces them (<see cref="TemperaturesRead"/>), for the
/// host to show wherever it likes. Polls only while the host says the popup is
/// visible, so nothing is read (and neither chip is asked anything) while the
/// app sits in the tray.
/// </summary>
internal sealed class FanSection : SectionPanel
{
    // Once a second: the temperatures in the window follow the laptop as it happens.
    private const int PollIntervalMilliseconds = 1_000;

    private readonly FanTelemetryService _telemetryService;
    private readonly ICpuTemperatureSource _cpuTemperature;
    private readonly IGpuTemperatureSource _gpuTemperature;
    private readonly HashSet<string> _loggedTemperatureFailures = [];
    private readonly IPowerSource _powerSource;
    private readonly ThemedToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _pollTimer = new()
    {
        Interval = PollIntervalMilliseconds
    };
    private readonly SettingRow _row;
    private readonly ToggleSwitch _maxSwitch;

    // The fans' last speeds, null until read.
    private int? _cpuRpm;
    private int? _gpuRpm;

    // Set while the switch is moved to show the laptop's state, not by a click.
    private bool _showingState;

    private PerformanceState _performanceState = PerformanceState.Unknown;
    private bool _isMaxAvailable;
    private MaxFanMethod _maxFanMethod = MaxFanMethod.ControllerFlag;
    private bool _isPolling;
    private bool _refreshInProgress;

    public FanSection(
        FanTelemetryService telemetryService,
        IPowerSource powerSource,
        ICpuTemperatureSource cpuTemperature,
        IGpuTemperatureSource gpuTemperature)
    {
        _telemetryService = telemetryService;
        _powerSource = powerSource;
        _cpuTemperature = cpuTemperature;
        _gpuTemperature = gpuTemperature;

        // Max fan, as in Synapse's "Max Fan Speed Mode": on, the fans run flat
        // out however the laptop is used; off, the system sets their speed.
        // Only offered when the laptop allows it; turning it off always is.
        _maxSwitch = new ToggleSwitch { AccessibleName = L.T("Max RPM"), Name = "MaxFanSwitch" };
        _maxSwitch.CheckedChanged += (_, _) =>
        {
            if (!_showingState)
                RequestMaxFan(_maxSwitch.Checked);
        };

        _row = new SettingRow("Max RPM", string.Empty, NavIcon.Fan) { Dock = DockStyle.Top, Divided = true };
        _row.Add(_maxSwitch).FlipsOnClick(_maxSwitch);
        Controls.Add(_row);

        ShowReadings();
        UpdateSwitch();

        _pollTimer.Tick += PollTimer_Tick;
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>The row's height.</summary>
    public int RowHeight => _row.Height;

    /// <summary>False once the fan speeds have been taken away.</summary>
    // Tracked in a field: Control.Visible reads false whenever the popup is hidden.
    public bool AreReadingsShown => !_readingsHidden;

    private bool _readingsHidden;

    /// <summary>
    /// Takes the CPU and GPU fan speeds away for a laptop that does not report
    /// them, rather than showing placeholders that never fill in.
    /// </summary>
    public void HideReadings()
    {
        if (_readingsHidden)
            return;

        _readingsHidden = true;
        ShowReadings();
    }

    /// <summary>Raised when the user asks for max fan speed on (true) or off (false). The host performs it.</summary>
    public event EventHandler<bool>? MaxFanRequested;

    /// <summary>Raised after every poll with the temperatures read, each null when there is no reading for it.</summary>
    public event EventHandler<TemperatureReading>? TemperaturesRead;

    /// <summary>Tells the switch which performance mode the laptop is in, since Max depends on it.</summary>
    public void ShowPerformanceState(PerformanceState state)
    {
        _performanceState = state;
        UpdateSwitch();
    }

    /// <summary>Starts polling and takes an immediate reading.</summary>
    public void StartPolling()
    {
        _isPolling = true;
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public void StopPolling()
    {
        _isPolling = false;
        _pollTimer.Stop();

        // A reading only means something for as long as it is being refreshed.
        // Clearing it now means reopening the popup never shows a temperature
        // from minutes ago while the first fresh one is on its way.
        TemperaturesRead?.Invoke(this, TemperatureReading.None);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTimer_Tick;
            _pollTimer.Dispose();
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e) =>
        PostToUi(UpdateSwitch);

    // Shows what the laptop is doing (on when its Max flag is) and whether Max
    // can be turned on. When it cannot (see PowerProfileRules.CanUseMaxFan)
    // and it is off, the switch is greyed out and hovering the row says why;
    // on, it can always be turned off.
    private void UpdateSwitch()
    {
        var pluggedIn = PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);
        _isMaxAvailable = PowerProfileRules.CanUseMaxFan(_performanceState, pluggedIn, _maxFanMethod);

        var on = _performanceState.MaxFan == true;
        _showingState = true;
        _maxSwitch.Checked = on;
        _showingState = false;
        _maxSwitch.Enabled = on || _isMaxAvailable;

        var reason = on || _isMaxAvailable ? string.Empty : _maxFanMethod switch
        {
            MaxFanMethod.ControllerFlag => L.T("Needs Custom mode, plugged in"),
            MaxFanMethod.ManualFan => L.T("Needs to be plugged in, and not in Silent mode"),
            _ => L.T("This laptop does not support max fan speed")
        };

        foreach (var part in new Control[] { _row, _row.Title, _row.Hint })
            _toolTip.SetToolTip(part, reason);
    }

    /// <summary>Tells the switch how this laptop runs its fans flat out, which decides when Max is offered.</summary>
    public void SetMaxFanMethod(MaxFanMethod method)
    {
        _maxFanMethod = method;
        UpdateSwitch();
    }

    private void RequestMaxFan(bool enabled)
    {
        // Already in that state: nothing to change.
        if (enabled == (_performanceState.MaxFan == true))
            return;

        // Turning it on needs Max to be available; turning it off never does.
        // Refused, the switch goes back to what the laptop is doing.
        if (enabled && !_isMaxAvailable)
        {
            PostToUi(UpdateSwitch);
            return;
        }

        MaxFanRequested?.Invoke(this, enabled);
    }

    private async void PollTimer_Tick(object? sender, EventArgs e) =>
        await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshInProgress)
            return;

        _refreshInProgress = true;

        try
        {
            // Read together, so the temperatures never trail the fan speeds.
            var temperatures = Task.Run(ReadTemperatures);
            var reading = _readingsHidden ? null : await _telemetryService.ReadAsync();
            var temperature = await temperatures;

            if (!_isPolling)
                return;

            TemperaturesRead?.Invoke(this, temperature);

            // A laptop that turns out not to report fan speeds loses them.
            if (!_telemetryService.IsSupported)
            {
                HideReadings();
                return;
            }

            if (reading is null)
                return;

            _cpuRpm = reading.CpuFanRpm;
            _gpuRpm = reading.GpuFanRpm;
            ShowReadings();
        }
        catch (Exception exception)
        {
            AppLog.Error("Fan telemetry read failed unexpectedly.", exception);

            if (_isPolling)
            {
                _cpuRpm = _gpuRpm = null;
                ShowReadings();
                TemperaturesRead?.Invoke(this, TemperatureReading.None);
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    // Each source has its own safety net: a temperature that cannot be read is
    // just missing, and can never take the fan speeds (or the other
    // temperature) down with it.
    private TemperatureReading ReadTemperatures() => new(
        SafeRead("CPU temperature", _cpuTemperature.ReadCelsius),
        SafeRead("GPU temperature", _gpuTemperature.ReadCelsius));

    private double? SafeRead(string name, Func<double?> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception)
        {
            // Polled every couple of seconds: log a failing source once, not every time.
            if (_loggedTemperatureFailures.Add(name))
                AppLog.Error($"Could not read the {name}.", exception);

            return null;
        }
    }

    // Under the name: the CPU and GPU fans' speeds now, or, on a laptop that
    // does not report them, what Max does.
    private void ShowReadings() =>
        _row.Hint.Text = _readingsHidden
            ? L.T("Always runs at 100%, however it is used")
            : L.F("Now {0} · {1} RPM", Rpm(_cpuRpm), Rpm(_gpuRpm));

    private static string Rpm(int? rpm) => rpm?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "--";
}
