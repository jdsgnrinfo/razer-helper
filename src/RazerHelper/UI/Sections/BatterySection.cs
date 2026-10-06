using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The battery's charge limit, as one settings row: its name with where the
/// power comes from and the charge under it ("Plugged in · 84%"), the slider,
/// and the chosen limit at the right. Reports its results through events so
/// the host decides how to persist and display them.
/// </summary>
internal sealed class BatterySection : SectionPanel
{
    private readonly BatteryChargeLimitService _chargeLimitService;
    private readonly IPowerSource _powerSource;
    private readonly SettingRow _row;
    private readonly ThemedSlider _slider;
    private readonly Label _limitLabel;
    private readonly ThemedToolTip _toolTip = new();

    // The limit the EC last confirmed (or the saved one, until the first
    // write). Null means nothing has been applied yet.
    private int? _appliedLimit;
    private bool _updateInProgress;
    private bool _isSupported = true;

    public BatterySection(BatteryChargeLimitService chargeLimitService, int? savedLimit, IPowerSource powerSource)
    {
        _chargeLimitService = chargeLimitService;
        _powerSource = powerSource;
        _appliedLimit = savedLimit;

        var initialLimit = BatteryLimitRange.Normalize(savedLimit ?? BatteryLimitRange.NoLimit);

        _slider = new ThemedSlider(BatteryLimitRange.Minimum, BatteryLimitRange.Maximum, BatteryLimitRange.Step)
        {
            AccessibleName = L.T("Charge limit"),
            Format = value => $"{value} %",
            Height = S(30)
        };

        _limitLabel = new RollingLabel
        {
            AutoSize = false,
            Font = SemiBoldFont(14),
            ForeColor = Color.White,
            Size = new Size(S(50), S(24)),
            TextAlign = ContentAlignment.MiddleRight
        };

        _slider.ValueChanged += (_, _) => _limitLabel.Text = $"{_slider.Value} %";
        _slider.Value = initialLimit;
        _limitLabel.Text = $"{_slider.Value} %";
        _slider.Committed += async (_, _) => await CommitAsync();

        _row = new SettingRow("Charge limit", string.Empty, NavIcon.ChargeLimit) { Dock = DockStyle.Top, TextWidth = S(150) };
        _row.Add(_limitLabel).Fill(_slider);
        Controls.Add(_row);

        UpdatePowerLabel();
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>The row's height.</summary>
    public int RowHeight => _row.Height;

    /// <summary>
    /// Re-reads the battery charge. Windows only tells listeners when the power
    /// source changes, not on every percentage step, so the host calls this
    /// whenever the popup is shown.
    /// </summary>
    public void RefreshPowerStatus() => UpdatePowerLabel();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e) =>
        PostToUi(UpdatePowerLabel);

    // "Plugged in · 84%" or "On battery · 62%"; without a reading, just the source.
    private void UpdatePowerLabel()
    {
        var source = _powerSource.IsPluggedIn switch
        {
            true => L.T("Plugged in"),
            false => L.T("On battery"),
            null => string.Empty
        };

        _row.Hint.Text = _powerSource.BatteryPercent is { } percent
            ? source.Length > 0 ? $"{source} · {percent}%" : $"{percent}%"
            : source;
    }

    /// <summary>Raised after the EC confirms a new limit.</summary>
    public event EventHandler<int>? ChargeLimitApplied;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>
    /// For a model whose controller acknowledges a charge limit but never
    /// applies it: the slider is disabled and nothing is sent, so the app never
    /// claims a limit the battery ignores. Hovering the slider says why, like
    /// the other unavailable controls.
    /// </summary>
    public void MarkUnsupported()
    {
        if (!_isSupported)
            return;

        _isSupported = false;

        // Drawn as unavailable but not disabled, so its tooltip still shows
        // (WinForms shows none on a disabled control), as on Max.
        _slider.Available = false;
        _limitLabel.ForeColor = SubtleTextColor;
        _slider.Cursor = Cursors.Default;
        _toolTip.SetToolTip(_slider, L.T("This laptop does not support a battery charge limit"));
    }

    /// <summary>Re-applies the saved limit, e.g. after a reboot.</summary>
    public async Task RestoreAsync()
    {
        if (!_isSupported || _appliedLimit is not int savedLimit)
            return;

        try
        {
            await _chargeLimitService.SetChargeLimitAsync(BatteryLimitRange.Normalize(savedLimit));
        }
        catch (Exception exception)
        {
            AppLog.Error(
                L.F("Could not restore the saved battery charge limit ({0}%).", savedLimit),
                exception);
        }
    }

    private async Task CommitAsync()
    {
        if (_updateInProgress || !_isSupported)
            return;

        var requestedLimit = _slider.Value;
        var previousLimit = _appliedLimit;

        // MouseUp and KeyUp fire for any click or key press, not only when the
        // value moved. The applied limit is only updated after a confirmed
        // write, so matching it means the EC already has this value.
        if (requestedLimit == previousLimit)
            return;

        _updateInProgress = true;
        _slider.Enabled = false;

        try
        {
            await _chargeLimitService.SetChargeLimitAsync(requestedLimit);

            _appliedLimit = requestedLimit;
            ChargeLimitApplied?.Invoke(this, requestedLimit);

            StatusChanged?.Invoke(this, new SectionStatus(requestedLimit == BatteryLimitRange.NoLimit
                ? L.T("Battery charge limit disabled. Charging is allowed to 100%.")
                : L.F("Battery charge limit set to {0}%.", requestedLimit)));
        }
        catch (Exception exception)
        {
            AppLog.Error(
                L.F("Battery charge-limit change to {0}% failed.", requestedLimit),
                exception);

            _slider.Value = BatteryLimitRange.Normalize(previousLimit ?? BatteryLimitRange.NoLimit);

            StatusChanged?.Invoke(this, new SectionStatus(
                L.T("Could not change the battery charge limit."),
                IsError: true));
        }
        finally
        {
            _slider.Enabled = true;
            _updateInProgress = false;
        }
    }
}
