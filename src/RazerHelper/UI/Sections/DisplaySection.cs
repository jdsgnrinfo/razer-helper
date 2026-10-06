using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Refresh-rate controls: a fixed rate or Auto, which follows the power
/// source. Reports the chosen mode through an event so the host can persist it.
/// </summary>
internal sealed class DisplaySection : SectionPanel
{
    private readonly DisplayService _displayService;
    private readonly IPowerSource _powerSource;
    private readonly DisplayRefreshMode? _savedMode;
    private readonly int _fastHz;
    private readonly int? _secondHz;
    private readonly Label _statusLabel;
    private readonly Dictionary<DisplayRefreshMode, Button> _buttons = [];
    private readonly FullscreenGuard _fullscreenGuard;
    private readonly ThemedToolTip _toolTip = new();

    // Only runs while an automatic change is on hold behind a fullscreen game.
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 10_000 };

    private DisplayRefreshMode? _selectedMode;

    public DisplaySection(
        DisplayService displayService,
        IPowerSource powerSource,
        DisplayRefreshMode? savedMode,
        IFullscreenDetector? fullscreenDetector = null)
    {
        _fullscreenGuard = new FullscreenGuard(fullscreenDetector ?? new NoFullscreenDetector());
        _retryTimer.Tick += RetryTimer_Tick;
        _displayService = displayService;
        _powerSource = powerSource;
        _savedMode = savedMode;

        var detectedRates = displayService.GetAvailableRefreshRates();
        // 120 if detection fails, matching what every offered panel supported before this was detected.
        _fastHz = detectedRates.Count > 0 ? detectedRates[^1] : 120;
        // Ascending and distinct, so the second-to-last entry is the next rate down from the fastest.
        _secondHz = detectedRates.Count >= 2 ? detectedRates[^2] : null;

        // The title is short; the detected mode ("1920x1080 (120 Hz)") needs the room.
        var header = CreateHeaderLayout(40F, 60F);

        // Lined up with the buttons, inside their glow room.
        header.Height = SectionHeaderHeight - GlowRoom;
        header.Padding = new Padding(GlowRoom, 0, GlowRoom, S(12) - GlowRoom);

        _statusLabel = CreateHeaderValueLabel();
        _statusLabel.Text = L.T("Current: -- Hz");

        header.Controls.Add(CreateSectionLabel("Refresh rate", NavIcon.RefreshRate), 0, 0);
        header.Controls.Add(_statusLabel, 1, 0);

        var modes = OfferedModes();
        var grid = CreateButtonGrid(modes.Select(mode => mode.Label).ToArray(), "RefreshRateButton", glowRoom: GlowRoom);

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = modes.First(candidate => candidate.Label == (string)button.Tag!);
            _buttons[mode] = button;
            button.Font = ProfileButtonFont;
            button.Click += (_, _) => SelectMode(mode);

            // Just the icon on the button; its name ("60 Hz", "Auto") under the pointer.
            if (button is RoundedButton rounded)
                rounded.Icon = RefreshRateIcon.For(mode.FixedHz);

            _toolTip.SetToolTip(button, button.Text);
        }

        Controls.Add(grid);
        Controls.Add(header);

        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Raised after a mode is chosen and applied.</summary>
    public event EventHandler<DisplayRefreshMode>? DisplayModeChanged;

    /// <summary>Auto, then 60 Hz (always offered), the next rate down from the fastest if it differs, and the fastest rate if it differs.</summary>
    private IReadOnlyList<DisplayRefreshMode> OfferedModes()
    {
        var modes = new List<DisplayRefreshMode> { DisplayRefreshMode.Auto, DisplayRefreshMode.Fixed(DisplayRefreshMode.OnBatteryHz) };

        if (_secondHz is int secondHz && secondHz != DisplayRefreshMode.OnBatteryHz)
            modes.Add(DisplayRefreshMode.Fixed(secondHz));

        if (_fastHz != DisplayRefreshMode.OnBatteryHz)
            modes.Add(DisplayRefreshMode.Fixed(_fastHz));

        return modes;
    }

    /// <summary>Re-selects the saved mode, applies it if it is Auto, and shows the current mode.</summary>
    public void Restore()
    {
        if (_savedMode is not null && _buttons.ContainsKey(_savedMode))
        {
            Select(_savedMode);

            if (_savedMode.IsAuto)
                _ = ApplyAutoAsync();
        }

        // Keep the "waiting for the game" note if a change was just put on hold.
        if (!_fullscreenGuard.IsWaiting)
            UpdateDisplayStatus();
    }

    /// <summary>
    /// Shows what the display is doing now. Called when the popup opens, because a
    /// game may have changed the resolution or refresh rate since it was last read.
    /// Also lets a change that was held back behind a game go ahead if the game is gone.
    /// </summary>
    public void RefreshStatus()
    {
        if (_fullscreenGuard.ReadyToRetry)
            _ = ApplyAutoAsync();
        else if (!_fullscreenGuard.IsWaiting)
            UpdateDisplayStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _retryTimer.Stop();
            _retryTimer.Tick -= RetryTimer_Tick;
            _retryTimer.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    // The button answers at once; Windows changes the rate on a worker
    // thread, which takes a moment while the screen re-syncs, so the window
    // never freezes meanwhile.
    private async void SelectMode(DisplayRefreshMode mode)
    {
        if (mode.IsAuto)
        {
            Select(mode);
            DisplayModeChanged?.Invoke(this, mode);
            await ApplyAutoAsync();
            return;
        }

        // A rate the user picked replaces anything that was on hold.
        _fullscreenGuard.Cancel();
        _retryTimer.Stop();

        var previous = _selectedMode;
        Select(mode);

        var (applied, message) = await SetRateAsync(mode.FixedHz!.Value);

        if (!applied)
        {
            // Back to what was chosen before, unless another button was clicked meanwhile.
            if (_selectedMode == mode)
            {
                _selectedMode = previous;
                HighlightSelected(_buttons.Values, previous is null ? null : _buttons[previous]);
            }

            _statusLabel.Text = message;
            return;
        }

        DisplayModeChanged?.Invoke(this, mode);
        UpdateDisplayStatus();
    }

    // One change at a time, in the order asked, off the UI thread.
    private Task _rateChange = Task.CompletedTask;

    private Task<(bool Applied, string Message)> SetRateAsync(int hertz)
    {
        var before = _rateChange;
        var change = Task.Run(async () =>
        {
            await before.ConfigureAwait(false);
            var applied = _displayService.TrySetInternalRefreshRate(hertz, out var message);
            return (applied, message);
        });

        _rateChange = change;
        return change;
    }

    private void Select(DisplayRefreshMode mode)
    {
        _selectedMode = mode;
        HighlightSelected(_buttons.Values, _buttons[mode]);
    }

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e)
    {
        if (_selectedMode?.IsAuto == true)
            PostToUi(() => _ = ApplyAutoAsync());
    }

    private async Task ApplyAutoAsync()
    {
        var isPluggedIn = _powerSource.IsPluggedIn;

        if (isPluggedIn is null)
        {
            _statusLabel.Text = L.T("Auto: power source unavailable.");
            return;
        }

        // A fullscreen game has the screen: changing the refresh rate under it can
        // make it flicker or drop out. Hold the change, and try again until it is gone.
        if (!_fullscreenGuard.CanApplyNow())
        {
            _statusLabel.Text = L.T("Auto: waiting for the game");
            _retryTimer.Start();
            return;
        }

        _retryTimer.Stop();

        var targetHz = DisplayRefreshMode.Auto.TargetHz(isPluggedIn.Value, _fastHz);

        if (_displayService.GetInternalDisplayInfo()?.RefreshRateHz != targetHz)
        {
            var (applied, message) = await SetRateAsync(targetHz);

            if (!applied)
            {
                _statusLabel.Text = message;
                return;
            }
        }

        UpdateDisplayStatus();
    }

    private void RetryTimer_Tick(object? sender, EventArgs e)
    {
        if (_fullscreenGuard.ReadyToRetry)
            _ = ApplyAutoAsync();
    }

    private void UpdateDisplayStatus()
    {
        var displayInfo = _displayService.GetInternalDisplayInfo();

        // Just the facts beside the "Display" title: "1920x1080 (120 Hz)".
        _statusLabel.Text = displayInfo is null
            ? L.T("Not available")
            : $"{displayInfo.Width}x{displayInfo.Height} ({displayInfo.RefreshRateHz} Hz)";
    }
}
