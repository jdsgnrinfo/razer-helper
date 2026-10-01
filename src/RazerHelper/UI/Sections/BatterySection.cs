using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The power source and battery charge ("Plugged in · 84%") in the header,
/// and the charge-limit slider with the chosen limit under it. Reports its
/// results through events so the host decides how to persist and display them.
/// </summary>
internal sealed class BatterySection : SectionPanel
{
    /// <summary>The title row: taller than the others, to fit the "More info" button, then the 8px gap.</summary>
    private static int HeaderHeight => S(30 + 6);

    /// <summary>The header and the slider with the chosen limit beside it.</summary>
    public static int ContentHeight => HeaderHeight + S(24);

    /// <summary>Raised when the user asks for the Battery details window. The host opens it.</summary>
    public event EventHandler? DetailsRequested;

    private readonly BatteryChargeLimitService _chargeLimitService;
    private readonly IPowerSource _powerSource;
    private readonly ThemedSlider _slider;
    private readonly Label _powerLabel;
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

        // Header, right: how full the battery is and where the power comes
        // from ("84% (Plugged in)"), then More info, which opens the Battery
        // details window.
        _powerLabel = CreateHeaderValueLabel();
        _powerLabel.Font = DesignFont(16, FontStyle.Bold);
        _powerLabel.Dock = DockStyle.None;
        _powerLabel.Anchor = AnchorStyles.Right;

        var details = CreateSmallButton("More info");
        details.Anchor = AnchorStyles.Right;
        details.Margin = new Padding(S(12), 0, 0, 0);
        details.Click += (_, _) => DetailsRequested?.Invoke(this, EventArgs.Empty);

        var header = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Height = HeaderHeight,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, S(6)),
            RowCount = 1
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        header.Controls.Add(CreateSectionLabel("Battery Charge Limit"), 0, 0);
        header.Controls.Add(_powerLabel, 1, 0);
        header.Controls.Add(details, 2, 0);

        // The slider, with the chosen limit at its right, as the lighting sliders show brightness.
        _slider = new ThemedSlider(BatteryLimitRange.Minimum, BatteryLimitRange.Maximum, BatteryLimitRange.Step)
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        _limitLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = DesignFont(16, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleRight
        };

        _slider.ValueChanged += (_, _) => _limitLabel.Text = $"{_slider.Value} %";
        _slider.Value = initialLimit;
        _limitLabel.Text = $"{_slider.Value} %";

        var limit = CreateTwoColumnLayout(100F, 0F);
        limit.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, S(68));
        limit.Dock = DockStyle.Top;
        limit.Height = S(24);
        limit.Controls.Add(_slider, 0, 0);
        limit.Controls.Add(_limitLabel, 1, 0);

        _slider.Committed += async (_, _) => await CommitAsync();

        // Dock order: the last added docks first, so the header is on top.
        Controls.Add(limit);
        Controls.Add(header);

        UpdatePowerLabel();
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

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

    // "84% (Plugged in)" or "62% (On battery)"; without a reading, just the source.
    private void UpdatePowerLabel()
    {
        var source = _powerSource.IsPluggedIn switch
        {
            true => L.T("Plugged in"),
            false => L.T("On battery"),
            null => string.Empty
        };

        _powerLabel.Text = _powerSource.BatteryPercent is { } percent
            ? source.Length > 0 ? $"{percent}% ({source})" : $"{percent}%"
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
