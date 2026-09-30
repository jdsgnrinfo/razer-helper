using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// CPU and GPU fan speed readout. Every poll it also reads the CPU and GPU
/// temperatures and announces them (<see cref="TemperaturesRead"/>), for the
/// host to show wherever it likes. Polls only while the host says the popup is
/// visible, so nothing is read (and neither chip is asked anything) while the
/// app sits in the tray.
/// </summary>
internal sealed class FanSection : SectionPanel
{
    private const int PollIntervalMilliseconds = 2_000;

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
    private readonly Label _cpuFanLabel;
    private readonly Label _gpuFanLabel;
    private readonly TableLayoutPanel _readings;
    private readonly Button[] _modeButtons;
    private readonly Button _autoButton;
    private readonly Button _maxButton;

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

        _cpuFanLabel = CreateReadingLabel($"{L.T("CPU Fan")}: -- RPM");
        _gpuFanLabel = CreateReadingLabel($"{L.T("GPU Fan")}: -- RPM");

        // Each reading sits above its own button: CPU over Auto, GPU over Max,
        // side by side. The columns are the same width as the button cells
        // below, so the text lines up with the left edge of each button.
        var readings = _readings = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Height = ReadingsHeight,
            Margin = Padding.Empty,
            Padding = S(new Padding(0, 0, 0, 2)),
            RowCount = 1
        };

        readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        readings.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        readings.Controls.Add(_cpuFanLabel, 0, 0);
        readings.Controls.Add(_gpuFanLabel, 1, 0);

        // Automatic | Max RPM, as in Synapse's "Max Fan Speed Mode", side by
        // side as two options with a line each on what they do. Max is only
        // offered when the laptop allows it; Automatic turns it off again.
        _autoButton = new RadioOption(L.T("Automatic RPM"), L.T("The system picks the speed based on how it is used"))
        {
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, S(12), 0),
            Name = "AutoFanModeButton"
        };
        _maxButton = new RadioOption(L.T("Max RPM"), L.T("Always runs at 100%, however it is used"))
        {
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(S(12), 0, 0, 0),
            Name = "MaxFanModeButton"
        };
        _modeButtons = [_autoButton, _maxButton];

        var modeGrid = CreateTwoColumnLayout(50F, 50F);
        modeGrid.Controls.Add(_autoButton, 0, 0);
        modeGrid.Controls.Add(_maxButton, 1, 0);

        _autoButton.Click += (_, _) => RequestMaxFan(false);
        _maxButton.Click += (_, _) => RequestMaxFan(true);
        UpdateModeButtons();

        // Dock order: the header docks first, then the readings, and the mode
        // buttons fill what is left.
        Controls.Add(modeGrid);
        Controls.Add(readings);
        Controls.Add(CreateSectionHeader("Fans", string.Empty));

        _pollTimer.Tick += PollTimer_Tick;
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>The height of the fan speed line, which goes away on a laptop that does not report fan speeds.</summary>
    public static int ReadingsHeight => S(24);

    /// <summary>False once the fan speed line has been removed.</summary>
    // Tracked in a field: Control.Visible reads false whenever the popup is hidden.
    public bool AreReadingsShown => !_readingsHidden;

    private bool _readingsHidden;

    /// <summary>Raised once, when the fan speed line is removed, so the host can shrink the row.</summary>
    public event EventHandler? ReadingsHidden;

    /// <summary>
    /// Removes the CPU and GPU fan speed line for a laptop that does not report
    /// fan speeds, rather than showing placeholders that never fill in.
    /// </summary>
    public void HideReadings()
    {
        if (_readingsHidden)
            return;

        _readingsHidden = true;
        _readings.Visible = false;
        ReadingsHidden?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user asks for max fan speed on (true) or off (false). The host performs it.</summary>
    public event EventHandler<bool>? MaxFanRequested;

    /// <summary>Raised after every poll with the temperatures read, each null when there is no reading for it.</summary>
    public event EventHandler<TemperatureReading>? TemperaturesRead;

    /// <summary>Tells the fan buttons which performance mode the laptop is in, since Max depends on it.</summary>
    public void ShowPerformanceState(PerformanceState state)
    {
        _performanceState = state;
        UpdateModeButtons();
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
        PostToUi(UpdateModeButtons);

    // Shows what the laptop is doing (Max when its flag is on, otherwise Auto)
    // and whether Max can be chosen. When it cannot (see
    // PowerProfileRules.CanUseMaxFan) the button is drawn like a disabled one and hovering it says
    // why. It is deliberately still enabled underneath, because WinForms shows
    // no tooltip on a disabled control, so RequestMaxFan must refuse the click.
    private void UpdateModeButtons()
    {
        var pluggedIn = PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);
        _isMaxAvailable = PowerProfileRules.CanUseMaxFan(_performanceState, pluggedIn, _maxFanMethod);

        // Highlighting resets the text colors, so the unavailable look goes on after it.
        HighlightSelected(_modeButtons, _performanceState.MaxFan == true ? _maxButton : _autoButton);
        SetAvailability(
            _maxButton,
            _isMaxAvailable,
            _toolTip,
            _maxFanMethod switch
            {
                MaxFanMethod.ControllerFlag => L.T("Needs Custom mode, plugged in"),
                MaxFanMethod.ManualFan => L.T("Needs to be plugged in, and not in Silent mode"),
                _ => L.T("This laptop does not support max fan speed")
            });
    }

    /// <summary>Tells the fan buttons how this laptop runs its fans flat out, which decides when Max is offered.</summary>
    public void SetMaxFanMethod(MaxFanMethod method)
    {
        _maxFanMethod = method;
        UpdateModeButtons();
    }

    private void RequestMaxFan(bool enabled)
    {
        // Turning it on needs Max to be available; turning it off never does.
        if (enabled && !_isMaxAvailable)
            return;

        // Already in that state: nothing to change.
        if (enabled == (_performanceState.MaxFan == true))
            return;

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

            // A laptop that turns out not to report fan speeds loses the line.
            if (!_telemetryService.IsSupported)
            {
                HideReadings();
                return;
            }

            if (reading is null)
                return;

            ShowReading(_cpuFanLabel, "CPU Fan", reading.CpuFanRpm);
            ShowReading(_gpuFanLabel, "GPU Fan", reading.GpuFanRpm);
        }
        catch (Exception exception)
        {
            AppLog.Error("Fan telemetry read failed unexpectedly.", exception);

            if (_isPolling)
            {
                ShowReading(_cpuFanLabel, "CPU Fan", null);
                ShowReading(_gpuFanLabel, "GPU Fan", null);
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

    private static void ShowReading(Label label, string name, int? rpm) =>
        label.Text = rpm is null ? $"{L.T(name)}: -- RPM" : $"{L.T(name)}: {rpm} RPM";

    private static Label CreateReadingLabel(string text) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Font = DesignFont(12),
        ForeColor = Color.White,
        Margin = Padding.Empty,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };
}
