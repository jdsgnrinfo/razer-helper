using RazerHelper.Core.Diagnostics;
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
    /// <summary>The line under the slider that says what the limit is.</summary>
    public static int LimitLineHeight => S(22);

    /// <summary>The row with the "More info" button.</summary>
    public static int DetailsRowHeight => S(38);

    /// <summary>Raised when the user asks for the Battery details window. The host opens it.</summary>
    public event EventHandler? DetailsRequested;

    private readonly BatteryChargeLimitService _chargeLimitService;
    private readonly IPowerSource _powerSource;
    private readonly ThemedSlider _slider;
    private readonly Label _limitLabel;
    private readonly Label _powerLabel;
    private readonly FlowLayoutPanel _limitLine;
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

        var header = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Height = S(28),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Header, right: where the power comes from and how full the battery is.
        _powerLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            TextAlign = ContentAlignment.MiddleRight
        };

        header.Controls.Add(CreateSectionLabel("Battery Charge Limit", Glyph.Battery), 0, 0);
        header.Controls.Add(_powerLabel, 1, 0);

        // Under the slider: the chosen limit ("Limit: 80%").
        var charge = new Label
        {
            AutoSize = true,
            Dock = DockStyle.None,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Margin = Padding.Empty,
            Text = "Limit:",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _limitLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.None,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = RazerGreen,
            Margin = S(new Padding(4, 0, 0, 0)),
            Text = $"{initialLimit}%",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var limitLine = _limitLine = new FlowLayoutPanel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Height = LimitLineHeight,
            Margin = Padding.Empty,
            Padding = S(new Padding(4, 2, 0, 0)), // Line up with the section title.
            WrapContents = false
        };

        limitLine.Controls.Add(charge);
        limitLine.Controls.Add(_limitLabel);

        _slider = new ThemedSlider(BatteryLimitRange.Minimum, BatteryLimitRange.Maximum, BatteryLimitRange.Step)
        {
            Dock = DockStyle.Fill,
            Value = initialLimit
        };

        // Inset like the buttons in the other sections (their 4px margin), so
        // the slider's ends line up with theirs.
        var sliderRow = new Panel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Top,
            Height = _slider.Height,
            Margin = Padding.Empty,
            Padding = S(new Padding(4, 0, 4, 0))
        };

        sliderRow.Controls.Add(_slider);

        _slider.ValueChanged += (_, _) => _limitLabel.Text = $"{_slider.Value}%";
        _slider.Committed += async (_, _) => await CommitAsync();

        var spacer = new Panel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Top,
            Height = S(5)
        };

        // Opens the Battery details window (power in or out, time left, health).
        var details = UiControls.CreateActionButton("More info");
        details.Dock = DockStyle.Left;
        details.Margin = Padding.Empty;
        details.Width = S(120);
        details.Click += (_, _) => DetailsRequested?.Invoke(this, EventArgs.Empty);

        var detailsRow = new Panel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Top,
            Height = DetailsRowHeight,
            Margin = Padding.Empty,
            Padding = S(new Padding(4, 4, 0, 4)) // Line up with the slider and the buttons elsewhere.
        };

        detailsRow.Controls.Add(details);

        // Dock order: the last added docks first, so this reads top to bottom
        // as header, spacer, slider, limit line, details button.
        Controls.Add(detailsRow);
        Controls.Add(limitLine);
        Controls.Add(sliderRow);
        Controls.Add(spacer);
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

    // "Plugged in · 84%" or "On battery · 62%"; without a reading, just the source.
    private void UpdatePowerLabel()
    {
        var source = _powerSource.IsPluggedIn switch
        {
            true => "Plugged in",
            false => "On battery",
            null => string.Empty
        };

        _powerLabel.Text = _powerSource.BatteryPercent is { } percent
            ? source.Length > 0 ? $"{source} · {percent}%" : $"{percent}%"
            : source;
    }

    /// <summary>Raised after the EC confirms a new limit.</summary>
    public event EventHandler<int>? ChargeLimitApplied;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>False once the limit line under the slider has been removed.</summary>
    public bool IsLimitLineShown => _isSupported;

    /// <summary>Raised once, when the limit line is removed, so the host can shrink the row.</summary>
    public event EventHandler? LimitLineHidden;

    /// <summary>
    /// For a model whose controller acknowledges a charge limit but never
    /// applies it: the slider is disabled and nothing is sent, so the app never
    /// claims a limit the battery ignores. The limit line goes away, and
    /// hovering the slider says why, like the other unavailable controls.
    /// </summary>
    public void MarkUnsupported()
    {
        if (!_isSupported)
            return;

        _isSupported = false;
        _limitLine.Visible = false;

        // Drawn as unavailable but not disabled, so its tooltip still shows
        // (WinForms shows none on a disabled control), as on Max.
        _slider.Available = false;
        _slider.Cursor = Cursors.Default;
        _toolTip.SetToolTip(_slider, "This laptop does not support a battery charge limit");

        LimitLineHidden?.Invoke(this, EventArgs.Empty);
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
                $"Could not restore the saved battery charge limit ({savedLimit}%).",
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
                ? "Battery charge limit disabled. Charging is allowed to 100%."
                : $"Battery charge limit set to {requestedLimit}%."));
        }
        catch (Exception exception)
        {
            AppLog.Error(
                $"Battery charge-limit change to {requestedLimit}% failed.",
                exception);

            _slider.Value = BatteryLimitRange.Normalize(previousLimit ?? BatteryLimitRange.NoLimit);

            StatusChanged?.Invoke(this, new SectionStatus(
                "Could not change the battery charge limit.",
                IsError: true));
        }
        finally
        {
            _slider.Enabled = true;
            _updateInProgress = false;
        }
    }
}
