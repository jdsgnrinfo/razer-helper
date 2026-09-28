using System.ServiceProcess;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    // The popup's rows, top to bottom. They are added in this order (see AddRow).
    // The footer is always last and the Razer services row sits just above it:
    // anything new goes above both.
    private enum Row
    {
        Header,
        Performance,
        Fans,
        Display,
        Battery,
        Lighting,
        Services,
        Footer
    }

    // Each section row: its title row, what it shows, and the gap below it.
    private static int HeaderRowHeight => S(24) + SectionPanel.GapBelow;
    private static int PerformanceRowHeight => UiControls.SectionHeaderHeight + S(84) + SectionPanel.GapBelow;
    private static int ButtonRowHeight => UiControls.SectionHeaderHeight + S(40) + SectionPanel.GapBelow;
    private static int BatteryRowHeight => BatterySection.ContentHeight + SectionPanel.GapBelow;
    private static int LightingRowHeight => LightingSection.ContentHeight + SectionPanel.GapBelow;
    private static int FooterRowHeight => S(26);

    // Set for real once CheckForSupportedDevice() runs; a generic label until then.
    private string _modelText = DeviceSupportService.GenericModelName;

    private bool _allowClose;
    private bool _isResetting;
    private int _modalDepth;
    private bool _servicesRowShown;
    // The Custom window while it is open, so leaving Custom can close it.
    private CustomBoostForm? _customBoostForm;
    private readonly SettingsService _settingsService;
    // One EC connection shared by every service that talks to the hardware.
    private readonly IRazerTransport _transport;

    // The laptop found at startup, or null when none was.
    private readonly RazerLaptopModel? _model;
    private readonly IPowerSource _powerSource;
    private readonly IStartupRegistration _startupRegistration;
    private readonly bool _ownsDependencies;
    private readonly BatterySection _batterySection;
    private readonly LightingSection _lightingSection;
    private readonly DisplaySection _displaySection;
    private readonly FanSection _fanSection;
    private readonly PerformanceSection _performanceSection;
    private readonly ServicesSection _servicesSection;
    private readonly DgpuFreeUpCoordinator _dgpuCoordinator;
    private readonly FactoryReset _factoryReset;
    private readonly SynchronizationContext _uiContext;
    private readonly Label _headerStatusLabel = CreateHeaderStatusLabel();
    // Set in CreateAppHeader; the model name is appended to the title once known.
    private Label _titleLabel = null!;
    private readonly ThemedToolTip _toolTip = new();
    private readonly GlobalHotkey _hotkey = new();
    private readonly ProfileShortcuts _profileShortcuts = new();
    private readonly ProfileToast _profileToast = new();
    private readonly DisplayStateWatcher _displayWatcher = new();
    private readonly KeyboardScreenOffService _keyboardScreenOff;

    // The popup fades in on every showing and fades out before hiding
    // (see RequestHide and OnFadeTick). A WinForms timer: ticks on the UI thread.
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 15 };
    private double _fadeTarget = 1;
    private TableLayoutPanel _content = null!;
    private AppSettings _settings;

    /// <summary>The real app: talks to the actual laptop, Windows services and user profile.</summary>
    public TrayPopupForm()
        : this(
            new RazerHidTransport(),
            new PowerSourceService(),
            new WindowsServiceControl(),
            new SettingsService(),
            new RunKeyStartupRegistration(Environment.ProcessPath ?? Application.ExecutablePath),
            ownsDependencies: true,
            loginEntries: new RunKeyLoginEntries(),
            razerApps: new WindowsRazerApps(),
            gpuTemperature: new D3dkmtGpuTemperature(),
            fullscreenDetector: new WindowsFullscreenDetector())
    {
    }

    /// <summary>
    /// Everything the popup talks to, supplied from outside. The app passes
    /// the real ones; a test or a screenshot tool can pass fakes and look at the
    /// real popup without touching the laptop, its services or the user's settings.
    /// </summary>
    internal TrayPopupForm(
        IRazerTransport transport,
        IPowerSource powerSource,
        IServiceControl serviceControl,
        SettingsService settingsService,
        IStartupRegistration startupRegistration,
        bool ownsDependencies = false,
        IProcessControl? processControl = null,
        ILoginEntries? loginEntries = null,
        IRazerApps? razerApps = null,
        IGpuTemperatureSource? gpuTemperature = null,
        IFullscreenDetector? fullscreenDetector = null)
    {
        _transport = transport;
        _powerSource = powerSource;
        _settingsService = settingsService;
        _startupRegistration = startupRegistration;
        _ownsDependencies = ownsDependencies;

        // Read here, not in a field initializer: those run before the Form
        // base constructor, which is what may install the context.
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _settings = _settingsService.Load();

        // The CPU-side temperature comes from the laptop's controller, on the
        // same channel as the fan speeds. With no GPU to ask (tests, previews)
        // no GPU temperature is shown.
        _fanSection = new FanSection(
            new FanTelemetryService(_transport),
            _powerSource,
            new EcTemperatureService(_transport),
            gpuTemperature ?? new NoGpuTemperature());
        _fanSection.MaxFanRequested += FanSection_MaxFanRequested;
        _fanSection.ReadingsHidden += (_, _) => ResizeToFitRows();

        // Found before the sections are built: which modes are offered depends on it.
        _model = new DeviceSupportService().TryGetPresentModel(out var model) ? model : null;

        var maxFanMethod = _model?.MaxFan ?? MaxFanMethod.ControllerFlag;

        _performanceSection = new PerformanceSection(
            new PerformanceService(_transport, maxFanMethod),
            _powerSource,
            _settings.PluggedInProfile,
            _settings.OnBatteryProfile,
            maxFanMethod: maxFanMethod);
        _performanceSection.ProfileChanged += PerformanceSection_ProfileChanged;
        _performanceSection.CustomBoostRequested += (_, _) => ShowCustomBoost();
        _performanceSection.StatusChanged += Section_StatusChanged;
        _performanceSection.StateChanged += PerformanceSection_StateChanged;
        _performanceSection.AutoSwitchProfiles = _settings.AutoSwitchProfiles;
        _fanSection.TemperaturesRead += (_, reading) => _performanceSection.ShowTemperatures(reading);

        _displaySection = new DisplaySection(
            new DisplayService(),
            _powerSource,
            DisplayRefreshMode.Parse(_settings.DisplayMode),
            fullscreenDetector);
        _displaySection.DisplayModeChanged += DisplaySection_DisplayModeChanged;

        _batterySection = new BatterySection(
            new BatteryChargeLimitService(_transport),
            _settings.BatteryChargeLimit,
            _powerSource);
        _batterySection.ChargeLimitApplied += BatterySection_ChargeLimitApplied;
        _batterySection.StatusChanged += Section_StatusChanged;
        _batterySection.DetailsRequested += (_, _) => ShowBatteryDetails();

        var offersColor = _model?.HasKeyboardColor == true;
        var lightingService = new LightingService(_transport, offersColor);
        _lightingSection = new LightingSection(
            lightingService,
            offersColor,
            offersWave: _model?.HasWaveEffect ?? true);
        _lightingSection.StatusChanged += Section_StatusChanged;

        // The keyboard goes dark with the screen, and comes back with it.
        _keyboardScreenOff = new KeyboardScreenOffService(lightingService);
        _keyboardScreenOff.SetEnabled(_settings.KeyboardOffWithScreen);
        _displayWatcher.DisplayChanged += (_, on) => _keyboardScreenOff.OnDisplayChanged(on);
        // Left out (tests, previews) they are inert: no login entries, no programs.
        _servicesSection = new ServicesSection(
            new RazerSoftwareManager(
                new RazerServiceManager(serviceControl),
                new RazerLoginEntryManager(loginEntries ?? new NoLoginEntries()),
                razerApps ?? new NoRazerApps()),
            _settings.RazerServiceStartModes,
            _settings.RazerLoginApprovals);
        _servicesSection.HasServicesChanged += ServicesSection_HasServicesChanged;
        _servicesSection.StartModesRecorded += ServicesSection_StartModesRecorded;
        _servicesSection.LoginEntriesRecorded += ServicesSection_LoginEntriesRecorded;
        _servicesSection.ModalStateChanged += ServicesSection_ModalStateChanged;
        _servicesSection.StatusChanged += Section_StatusChanged;

        // Off unless the user turned it on. Reads the settings at the moment
        // it runs, so a change to the never-close list applies at once.
        var closer = new DgpuAppCloser(processControl ?? new WindowsProcessControl());
        _dgpuCoordinator = new DgpuFreeUpCoordinator(
            _powerSource,
            () => DgpuScanner.ScanAsync(_settings.NeverCloseApps),
            ConfirmCloseGpuAppsAsync,
            apps => closer.Close(apps, _settings.NeverCloseApps))
        {
            Enabled = _settings.CloseGpuAppsOnUnplug
        };

        _factoryReset = new FactoryReset(
            _settingsService,
            _startupRegistration,
            new PerformanceService(_transport, _model?.MaxFan ?? MaxFanMethod.ControllerFlag),
            new BatteryChargeLimitService(_transport));

        // Keep the tray popup's design surface stable across display scales.
        AutoScaleMode = AutoScaleMode.None;

        ApplyTheme();
        BuildView();
        CheckForSupportedDevice();

        _displaySection.Restore();
        _ = _batterySection.RestoreAsync();
        _ = _performanceSection.RestoreAsync();
        _ = _servicesSection.RefreshAsync();

        Deactivate += (_, _) => BeginInvoke(HideWhenInactive);

        TopMost = _settings.AlwaysOnTop;
        _hotkey.Pressed += (_, _) => ToggleFromShortcut();
        _profileShortcuts.Pressed += async (_, mode) => await SwitchModeFromShortcutAsync(mode);
        StartShortcut();
        StartProfileShortcuts();
        _fadeTimer.Tick += OnFadeTick;
    }

    /// <summary>
    /// The shortcut was pressed, possibly while a game has the screen. Brings
    /// the popup to the front and focuses it, or hides it if it already has focus.
    /// </summary>
    public void ToggleFromShortcut()
    {
        // A dialog (Settings) is open on top of the popup: leave it alone.
        if (_modalDepth > 0)
        {
            AppLog.Info($"{GlobalHotkey.Text} pressed while a dialog is open; ignored.");
            return;
        }

        if (Visible && ContainsFocus)
        {
            AppLog.Info($"{GlobalHotkey.Text} pressed: hiding the window.");
            RequestHide();
            return;
        }

        // Above everything for this showing even when "Always on top" is off; it
        // drops back to the setting when the popup is hidden.
        TopMost = true;

        // Reverses a fade-out that is still running (the window never left).
        FadeTo(1);

        if (!Visible)
        {
            Location = TaskbarPlacement.GetPopupLocation(Size);
            Show();
        }

        Activate();
        var gotFocus = SetForegroundWindow(Handle);

        // What happened, so a game that will not show the window can be told apart
        // from a shortcut that never arrived.
        AppLog.Info($"{GlobalHotkey.Text} pressed: showing the window (on top: {TopMost}, focus granted: {gotFocus}).");
    }

    // --- Fade in and out ---------------------------------------------------

    private const double FadeStep = 0.15; // opacity per 15 ms tick: ~120 ms in total

    // Hides the popup, but only once it has faded out, so it never pops away.
    // Safe to call repeatedly: while a fade-out is running it does nothing,
    // and while one is not it merely starts one.
    public void RequestHide()
    {
        if (!Visible || _fadeTarget == 0)
            return;

        FadeTo(0);
    }

    private void FadeTo(double target)
    {
        _fadeTarget = target;

        if (!_fadeTimer.Enabled)
            _fadeTimer.Start();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        if (Opacity < _fadeTarget)
        {
            Opacity = Math.Min(_fadeTarget, Opacity + FadeStep);

            if (Opacity >= _fadeTarget)
                _fadeTimer.Stop();

            return;
        }

        Opacity = Math.Max(_fadeTarget, Opacity - FadeStep);

        if (Opacity > _fadeTarget)
            return;

        _fadeTimer.Stop();

        // Only a finished fade-out completes the hide; a finished fade-in is done.
        if (_fadeTarget == 0 && Visible)
            Hide();
    }

    // The shortcut is always on. If another program already owns the key, say so
    // in the header instead of failing silently.
    private void StartShortcut()
    {
        if (_hotkey.TryRegister())
        {
            AppLog.Info($"Shortcut {GlobalHotkey.Text} is on.");
            return;
        }

        AppLog.Error($"Shortcut {GlobalHotkey.Text} could not be registered; another program uses it.");
        ShowStatus(new SectionStatus(L.F("{0} shortcut is used by another program.", GlobalHotkey.Text), IsError: true));
    }

    // Ctrl+Shift+F1/F2/F3 switch performance mode. One another program owns
    // is logged and said in the header, and the others still work.
    private void StartProfileShortcuts()
    {
        var taken = _profileShortcuts.TryRegister();

        if (taken.Count == 0)
        {
            AppLog.Info("Performance mode shortcuts are on.");
            return;
        }

        AppLog.Error($"Shortcuts {string.Join(", ", taken)} could not be registered; another program uses them.");
        ShowStatus(new SectionStatus(L.F("{0} shortcut is used by another program.", string.Join(", ", taken)), IsError: true));
    }

    // A mode shortcut was pressed, possibly in a game: switch as the button
    // would, then say how it went at the top right without taking the focus.
    private async Task SwitchModeFromShortcutAsync(PerformanceMode mode)
    {
        var (applied, status) = await _performanceSection.SelectFromShortcutAsync(mode);

        AppLog.Info($"Mode shortcut for {mode}: {status}");
        _profileToast.ShowNotice(PerformanceSection.GlyphFor(mode), L.T(mode.ToString()), status, applied);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    public void CloseForApplicationExit()
    {
        // Nothing is left watching the fans once the app is gone.
        _performanceSection.TurnOffMaxFanBeforeExit();
        _keyboardScreenOff.Restore(); // Never leave the keyboard dark behind.

        _allowClose = true;
        Close();
    }

    private void ApplyTheme()
    {
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;

        // Every showing fades in from nothing (see OnFadeTick).
        Opacity = 0;
        // The design's width: 552px of content inside a 32px margin.
        ClientSize = S(new Size(500, 600));
        Text = "RazerHelper";
        StartPosition = FormStartPosition.Manual;

        // The rounded corners and the outline come from Windows (see
        // OnHandleCreated), as on the Settings and Battery windows.
    }

    private void BuildView()
    {
        _content = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = S(new Padding(20)),
            RowCount = Enum.GetValues<Row>().Length
        };

        // Every row is a fixed height, so the popup's height is their sum.
        // The Performance and Razer services rows change height; see
        // ResizeToFitRows.
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var footer = new AppFooter();
        footer.SystemInfoRequested += (_, _) => ShowSystemInfo();
        footer.FreeUpGpuRequested += async (_, _) => await FreeUpGpuAsync();
        footer.SettingsRequested += (_, _) => ShowSettings();

        AddRow(Row.Header, CreateAppHeader(), HeaderRowHeight);
        AddRow(Row.Performance, _performanceSection, PerformanceRowHeight);
        AddRow(Row.Fans, _fanSection, ButtonRowHeight); // Sized in ResizeToFitRows.
        AddRow(Row.Display, _displaySection, ButtonRowHeight);
        AddRow(Row.Battery, _batterySection, BatteryRowHeight);
        AddRow(Row.Lighting, _lightingSection, LightingRowHeight);
        AddRow(Row.Services, _servicesSection, 0); // Grows when Razer's software is installed.
        AddRow(Row.Footer, footer, FooterRowHeight);

        Controls.Add(_content);

        ResizeToFitRows();
    }

    // Adds a row's height and its content together, so the two can never get out
    // of step. Rows must be added in the order of the Row enum.
    private void AddRow(Row row, Control content, float height)
    {
        System.Diagnostics.Debug.Assert((int)row == _content.RowStyles.Count, "Rows must be added in order.");

        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _content.Controls.Add(content, 0, (int)row);
    }

    private RowStyle RowStyleOf(Row row) => _content.RowStyles[(int)row];

    // Some rows change height: the Razer services row only exists when Razer's
    // software is installed, and Fans shows its speed line only on a laptop
    // that reports fan speeds. Grow or shrink the popup to match, then
    // re-anchor it to the taskbar so it does not end up floating or overlapping it.
    private void ResizeToFitRows()
    {
        RowStyleOf(Row.Services).Height = _servicesRowShown ? ServicesSection.RowHeight : 0;
        RowStyleOf(Row.Fans).Height = ButtonRowHeight +
            (_fanSection.AreReadingsShown ? FanSection.ReadingsHeight : 0);

        var contentHeight = _content.Padding.Vertical +
            _content.RowStyles.Cast<RowStyle>().Sum(row => row.Height);

        ClientSize = new Size(ClientSize.Width, (int)contentHeight);

        if (Visible)
            Location = TaskbarPlacement.GetPopupLocation(Size);
    }

    // Left: the app name and, once the laptop is identified, its model after a
    // dash, all in one bold white label. Middle: errors, in red, only while they
    // last. Right: the close button (an X). The bottom of the popup belongs to the Razer
    // services row, so this is where status text lives.
    private Control CreateAppHeader()
    {
        var header = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, SectionPanel.GapBelow),
            Padding = Padding.Empty,
            RowCount = 1
        };

        // The title hugs the left; errors take the slack; Close hugs the right.
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = DesignFont(13, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
        UpdateTitleText();
        header.Controls.Add(_titleLabel, 0, 0);
        header.Controls.Add(_headerStatusLabel, 1, 0);
        header.Controls.Add(CreateCloseButton(), 2, 0);

        return header;
    }

    // "RazerHelper" alone until the laptop is identified, then its model after
    // a dash, in the same bold white type.
    private void UpdateTitleText() =>
        _titleLabel.Text = _modelText == DeviceSupportService.GenericModelName
            ? "RazerHelper"
            : $"RazerHelper - {_modelText}";

    // The top-right close button: an X in the icons' style, a little larger
    // than the section icons (18px against 16) in a 28px target so it is easy
    // to hit. It hides the popup to the tray (fading out first); the app
    // keeps running.
    private GlyphButton CreateCloseButton()
    {
        var button = new GlyphButton(Glyph.Close, S(18))
        {
            AccessibleName = L.T("Close"),
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = S(new Size(24, 24))
        };

        button.Click += (_, _) => RequestHide();
        _toolTip.SetToolTip(button, L.T("Close (RazerHelper keeps running in the tray)"));
        return button;
    }

    // The user asked to close the apps keeping the dedicated GPU awake. The
    // list and the question come from the coordinator; this only tells them
    // when nothing was closed and why. The popup is held open meanwhile,
    // because the question window takes focus from it.
    private async Task FreeUpGpuAsync()
    {
        using var hold = KeepOpen();

        var outcome = await _dgpuCoordinator.FreeUpAsync();

        if (DgpuText.DescribeOutcome(outcome) is { } message)
            MessageBox.Show(this, message, L.T("Free up GPU"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // The coordinator works off the UI thread; the question has to be asked on it.
    private Task<bool> ConfirmCloseGpuAppsAsync(IReadOnlyList<DgpuApp> apps, bool dismissWhenPluggedIn)
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiContext.Post(_ =>
        {
            try
            {
                using var confirm = new GpuAppsConfirmForm(apps, _powerSource, dismissWhenPluggedIn);
                answer.SetResult(confirm.ShowDialog() == DialogResult.Yes);
            }
            catch (Exception exception)
            {
                answer.SetException(exception);
            }
        }, null);

        return answer.Task;
    }

    // Beside the popup and in front of it, like Settings, and the popup stays
    // open meanwhile. The window reads the battery only while it is open.
    private void ShowBatteryDetails()
    {
        using var detailsForm = new BatteryDetailsForm(BatteryReader.Read);

        detailsForm.PlaceBeside(this);
        detailsForm.TopMost = TopMost;

        using (KeepOpen())
            detailsForm.ShowDialog(this);
    }

    // The System information window, beside the popup like the Battery details.
    private void ShowSystemInfo()
    {
        using var infoForm = new SystemInfoForm(SystemInfoReader.Read);

        infoForm.PlaceBeside(this);
        infoForm.TopMost = TopMost;

        using (KeepOpen())
            infoForm.ShowDialog(this);
    }

    // Custom's CPU and GPU levels, beside the popup like Settings. Only one
    // at a time; it closes by itself if the laptop leaves Custom meanwhile.
    private void ShowCustomBoost()
    {
        if (_customBoostForm is not null)
            return;

        using var boostForm = new CustomBoostForm(_performanceSection.BoostSelectors);

        boostForm.PlaceBeside(this);
        boostForm.TopMost = TopMost;
        _customBoostForm = boostForm;

        // The fan poll already reads the CPU temperature for the header; pass it on too.
        void ShowTemperature(object? sender, TemperatureReading reading) => boostForm.ShowCpuTemperature(reading.CpuCelsius);
        _fanSection.TemperaturesRead += ShowTemperature;

        try
        {
            using (KeepOpen())
                boostForm.ShowDialog(this);
        }
        finally
        {
            _fanSection.TemperaturesRead -= ShowTemperature;
            _customBoostForm = null;
        }
    }

    private void ShowSettings()
    {
        using var settingsForm = new SettingsForm(_settings, _startupRegistration);

        settingsForm.AutoSwitchProfilesChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { AutoSwitchProfiles = enabled });
            _performanceSection.AutoSwitchProfiles = enabled;
        };

        settingsForm.HideWhenClickedAwayChanged += (_, enabled) =>
            SaveSettings(_settings with { HideWhenClickedAway = enabled });

        settingsForm.AlwaysOnTopChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { AlwaysOnTop = enabled });
            TopMost = enabled;
        };

        settingsForm.CloseGpuAppsOnUnplugChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { CloseGpuAppsOnUnplug = enabled });
            _dgpuCoordinator.Enabled = enabled;
        };

        settingsForm.KeyboardOffWithScreenChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { KeyboardOffWithScreen = enabled });
            _keyboardScreenOff.SetEnabled(enabled);
        };

        // Next to the popup, not over it, and in front of it: a popup that is
        // above other windows would otherwise hide the dialog it opened.
        settingsForm.PlaceBeside(this);
        settingsForm.TopMost = TopMost;

        using (KeepOpen())
            settingsForm.ShowDialog(this);

        if (settingsForm.ResetConfirmed)
            _ = ResetToDefaultsAsync();
        else if (settingsForm.LanguageChosen is { } language)
            RestartInLanguage(language);
    }

    // Every text is set as a window is built, so a new language needs a fresh start.
    private void RestartInLanguage(AppLanguage language)
    {
        SaveSettings(_settings with { Language = L.Code(language) });
        AppLog.Info($"Interface language changed to {language}; restarting.");

        if (!AppRestart.Relaunch())
        {
            using var hold = KeepOpen();
            MessageBox.Show(
                this,
                L.T("RazerHelper could not restart itself. Please close it from the tray icon and open it again."),
                "RazerHelper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        CloseForApplicationExit();
        Application.ExitThread();
    }

    // Confirmed in the Settings window. Clears everything, then restarts so the
    // whole app comes up as it would on a first run. A part that fails is
    // reported, but never stops the rest or the restart.
    private async Task ResetToDefaultsAsync()
    {
        _isResetting = true;
        using var hold = KeepOpen(); // The message boxes below must not hide the popup.

        var result = await _factoryReset.RunAsync(_settings);

        if (!result.Succeeded)
        {
            MessageBox.Show(
                this,
                L.T("Most of the reset worked, but not everything:") + "\r\n\r\n  - " + string.Join("\r\n  - ", result.Problems) +
                "\r\n\r\n" + L.T("RazerHelper will restart now. Details are in the log."),
                L.T("Reset to defaults"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        if (!AppRestart.Relaunch())
        {
            // Still running: keep what is in memory in step with the cleared
            // file, so the next change does not write the old settings back.
            _settings = FactoryReset.DefaultsKeepingServiceRecord(_settings);
            _isResetting = false;
            TopMost = _settings.AlwaysOnTop;

            MessageBox.Show(
                this,
                L.T("The reset is done, but RazerHelper could not restart itself. Please close it from the tray icon and open it again."),
                L.T("Reset to defaults"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        CloseForApplicationExit();
        Application.ExitThread();
    }

    private static Label CreateHeaderStatusLabel() => new()
    {
        AutoEllipsis = true,
        AutoSize = false,
        Dock = DockStyle.Fill,
        Font = GetDesignFont(FontFamilyName, 8.5F),
        ForeColor = SubtleTextColor,
        Margin = S(new Padding(12, 0, 10, 0)),
        Text = DeviceSupportService.GenericModelName,
        TextAlign = ContentAlignment.MiddleRight
    };

    // Successes are visible in the controls themselves, so only failures are
    // worth words. They replace the model name in red until the next result.
    private void ShowStatus(SectionStatus status)
    {
        // The model name lives in the title; this line carries errors only.
        _headerStatusLabel.ForeColor = status.IsError ? Color.IndianRed : SubtleTextColor;
        _headerStatusLabel.Text = status.IsError ? status.Message : string.Empty;

        if (!status.IsError)
            UpdateTitleText();

        // The label cuts long text short with an ellipsis; the tooltip has the rest.
        _toolTip.SetToolTip(_headerStatusLabel, status.IsError ? status.Message : string.Empty);
    }

    private void CheckForSupportedDevice()
    {
        if (_model is RazerLaptopModel model)
        {
            _modelText = model.Name;
            ShowStatus(new SectionStatus(model.Name));

            // Runs before RestoreAsync, so an ignored limit is never re-sent.
            if (!model.HasChargeLimit)
                _batterySection.MarkUnsupported();

            _fanSection.SetMaxFanMethod(model.MaxFan);

            if (!model.HasFanSpeeds)
                _fanSection.HideReadings();

            AppLog.Info(model.Verified
                ? L.F("{0} control interface found.", model.Name)
                : L.F("{0} control interface found (community-reported product id, not verified on this model).", model.Name));
            return;
        }

        AppLog.Error("No supported Razer laptop control interface found.");

        ShowStatus(new SectionStatus(
            L.T("No supported Razer laptop detected; fan and battery controls unavailable."),
            IsError: true));
    }

    private void SaveSettings(AppSettings settings)
    {
        // A reset is in progress: nothing may write the old settings back.
        if (_isResetting)
            return;

        _settings = settings;
        _settingsService.Save(settings);
    }

    private void DisplaySection_DisplayModeChanged(object? sender, DisplayRefreshMode mode) =>
        SaveSettings(_settings with { DisplayMode = mode.Label });

    private void PerformanceSection_ProfileChanged(object? sender, PowerProfileChange change) =>
        SaveSettings(change.PluggedIn
            ? _settings with { PluggedInProfile = change.Profile }
            : _settings with { OnBatteryProfile = change.Profile });

    private void BatterySection_ChargeLimitApplied(object? sender, int limit) =>
        SaveSettings(_settings with { BatteryChargeLimit = limit });

    private void ServicesSection_StartModesRecorded(object? sender, Dictionary<string, ServiceStartMode> modes) =>
        SaveSettings(_settings with { RazerServiceStartModes = modes });

    // An empty record means everything was restored, so there is nothing to keep.
    private void ServicesSection_LoginEntriesRecorded(object? sender, Dictionary<string, string> approvals) =>
        SaveSettings(_settings with { RazerLoginApprovals = approvals.Count > 0 ? approvals : null });

    // The fan buttons ask, the performance section does it (it owns the EC conversation).
    private void FanSection_MaxFanRequested(object? sender, bool enabled) =>
        _ = _performanceSection.SetMaxFanAsync(enabled);

    // Max fan speed only exists in Custom mode, so the fan buttons follow the performance state.
    /// <summary>Raised with the performance mode the laptop is in after every confirmed change (null when unknown), for the tray icon.</summary>
    internal event EventHandler<PerformanceMode?>? PerformanceModeChanged;

    private void PerformanceSection_StateChanged(object? sender, PerformanceState state)
    {
        _fanSection.ShowPerformanceState(state);
        PerformanceModeChanged?.Invoke(this, state.Mode);

        // The levels only mean something in Custom (the charger switching
        // profiles, or the Fn keys, can leave it while the window is open).
        if (state.Mode is PerformanceMode known && known != PerformanceMode.Custom)
            _customBoostForm?.Close();
    }

    private void ServicesSection_HasServicesChanged(object? sender, bool hasServices)
    {
        _servicesRowShown = hasServices;
        ResizeToFitRows();
    }

    // A dialog takes focus from the popup, which would hide it (and the dialog
    // with it) unless auto-hide is held off. Hold it for as long as the
    // returned value is not disposed.
    private OpenHold KeepOpen() => new(this);

    private sealed class OpenHold : IDisposable
    {
        private TrayPopupForm? _form;

        public OpenHold(TrayPopupForm form)
        {
            _form = form;
            form._modalDepth++;
        }

        public void Dispose()
        {
            if (_form is null)
                return;

            _form._modalDepth--;
            _form = null;
        }
    }

    // The elevation prompt takes focus from the popup too, and reports when it
    // starts and ends rather than being a scope, so it counts the same way.
    private void ServicesSection_ModalStateChanged(object? sender, bool isModal) =>
        _modalDepth += isModal ? 1 : -1;

    private void Section_StatusChanged(object? sender, SectionStatus status) =>
        ShowStatus(status);

    // "Clicked away" only means something once the popup has had focus to lose.
    // Windows sometimes declines to give focus to a window opened by a shortcut
    // while another program (a game) has the screen; without this the popup would
    // open and hide itself again at once.
    private void HideWhenInactive()
    {
        if (!_allowClose && _settings.HideWhenClickedAway && _hasBeenActive && _modalDepth == 0 && Visible && !ContainsFocus)
        {
            AppLog.Info("The window lost focus and hid itself (Hide when clicking away is on).");
            RequestHide();
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _hasBeenActive = true;
    }

    // A borderless window has no shadow of its own; ask for the standard one
    // so the popup lifts off whatever is behind it.
    protected override CreateParams CreateParams
    {
        get
        {
            const int ClassDropShadow = 0x00020000;

            var parameters = base.CreateParams;
            parameters.ClassStyle |= ClassDropShadow;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    protected override void Dispose(bool disposing)
    {
        // Sections unsubscribe from the shared services as they are disposed,
        // so those must go first; then the form releases what it created.
        base.Dispose(disposing);

        if (disposing)
        {
            _toolTip.Dispose();
            _hotkey.Dispose();
            _profileShortcuts.Dispose();
            _profileToast.Dispose();
            _displayWatcher.Dispose();
            _fadeTimer.Dispose();
            _dgpuCoordinator.Dispose();

            // Only what the form created itself; supplied dependencies belong to the caller.
            if (_ownsDependencies)
            {
                (_powerSource as IDisposable)?.Dispose();
                (_transport as IDisposable)?.Dispose();
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            RequestHide();
            return;
        }

        base.OnFormClosing(e);
    }

    // Clicking the tray icon to close the popup first takes focus from it, which
    // hides it; the click itself then arrives a moment later and would open it
    // again. The host asks this to tell that click apart from a real request.
    private const int JustHiddenMilliseconds = 300;
    private long _hiddenAtTicks = long.MinValue;
    private bool _hasBeenActive;

    /// <summary>True when the popup was hidden a moment ago, so a tray click now is the one that closed it.</summary>
    public bool WasJustHidden =>
        _hiddenAtTicks != long.MinValue && Environment.TickCount64 - _hiddenAtTicks < JustHiddenMilliseconds;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        // Each showing starts without focus, until Windows gives it.
        _hasBeenActive = false;

        if (!Visible)
        {
            _hiddenAtTicks = Environment.TickCount64;

            // A showing by shortcut may have raised it above everything; back to the setting.
            TopMost = _settings.AlwaysOnTop;
        }

        if (Visible)
        {
            FadeTo(1); // fade in; a fade-out still running reverses course
            _fanSection.StartPolling();

            // A game may have changed the resolution or refresh rate since it was last read.
            _displaySection.RefreshStatus();

            // The battery charge has moved on since it was last shown.
            _batterySection.RefreshPowerStatus();

            // Fn+P changes the mode without telling us; show what the EC has.
            _ = _performanceSection.RefreshAsync();

            // Lighting can be changed with the Fn keys or by other software.
            _ = _lightingSection.RefreshAsync();

            // Razer's services can be started or stopped from outside the app.
            _ = _servicesSection.RefreshAsync();
        }
        else
            _fanSection.StopPolling();
    }
}
