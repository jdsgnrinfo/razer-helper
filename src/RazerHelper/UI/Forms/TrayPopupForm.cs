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
    private bool _allowClose;
    private bool _isResetting;
    private int _modalDepth;
    // The one window open beside the popup: Custom's levels. The popup
    // stays usable meanwhile.
    private Form? _sideWindow;
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
    private readonly Sidebar _sidebar = new(LoadLogo(S(18)));
    private readonly Panel _pageHost = new() { AutoScroll = true, BackColor = BackgroundColor, Dock = DockStyle.Fill };
    private readonly Dictionary<DashboardPage, Pages.PageView> _pages = [];
    private DashboardPage _currentPage = DashboardPage.Performance;
    private Pages.OptimizePage _optimizePage = null!;
    // The laptop's name, which the sidebar shows whenever there is no error to show.
    private string _modelName = DeviceSupportService.GenericModelName;
    private readonly GlobalHotkey _hotkey = new();
    private readonly ProfileShortcuts _profileShortcuts = new();
    private readonly ProfileToast _profileToast = new();
    private readonly DisplayStateWatcher _displayWatcher = new();
    private readonly KeyboardScreenOffService _keyboardScreenOff;
    private readonly SilentPlanOverride _silentTurbo;
    // The Idle option, checked every couple of seconds whatever the window does.
    private readonly IPowerPlans _powerPlans;
    private readonly IdlePlanSwitcher _idleSwitcher;
    private readonly System.Windows.Forms.Timer _idleTimer = new() { Interval = 2_000 };

    // The popup fades in on every showing and fades out before hiding
    // (see RequestHide and OnFadeTick). A WinForms timer: ticks on the UI thread.
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 15 };
    private double _fadeTarget = 1;
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
            fullscreenDetector: new WindowsFullscreenDetector(),
            cpuBoost: PowerPlanValue.BoostMode(),
            cpuEfficiency: PowerPlanValue.EfficiencyPreference(),
            powerPlans: new WindowsPowerPlans(),
            idleClock: new WindowsIdleClock())
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
        IFullscreenDetector? fullscreenDetector = null,
        IPowerPlanValue? cpuBoost = null,
        IPowerPlanValue? cpuEfficiency = null,
        IPowerPlans? powerPlans = null,
        IIdleClock? idleClock = null)
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

        // Silent keeps the CPU at its base frequency, always: part of the
        // mode, with no switch. Balanced and Custom give the boost back. Left
        // out (tests, previews), the power plan is not touched.
        _silentTurbo = new SilentPlanOverride(
            cpuBoost ?? new NoPowerPlan(),
            0, // Boost off.
            "CPU boost",
            _settings.CpuBoostBeforeSilent,
            saved => SaveSettings(_settings with { CpuBoostBeforeSilent = saved }));

        GiveBackEnergyPreference(cpuEfficiency ?? new NoPowerPlan());

        // Off until the user turns it on in the Idle window. A plan a crash or
        // restart left switched comes back first. Left out (tests, previews),
        // no plan is touched.
        _powerPlans = powerPlans ?? new NoPowerPlans();
        _idleSwitcher = new IdlePlanSwitcher(
            _powerPlans,
            idleClock ?? new NeverIdle(),
            _settings.PlanBeforeIdle,
            saved => SaveSettings(_settings with { PlanBeforeIdle = saved }));
        _idleSwitcher.Restore();
        _idleSwitcher.Configure(_settings.IdleSwitch, _settings.IdleMinutes, _settings.IdlePlan);
        _idleTimer.Tick += (_, _) =>
        {
            _idleSwitcher.Tick();

            // In Silent, the boost stays off in whatever plan is active.
            _silentTurbo.Recheck();
        };
        _idleTimer.Start();

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
        if (_settings.ProfileShortcuts)
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

    // Per 15 ms tick: the whole fade takes as long as the other windows'
    // (WindowFade, 100 ms), quicker than the rest of the motion, so the
    // window is there as soon as it is asked for.
    private const double FadeStep = 15 / WindowFade.Milliseconds;

    // How far the fade is, 0 hidden to 1 shown, moving evenly; the window's
    // opacity follows it eased, slow at both ends.
    private double _fadeLevel;

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
        if (_fadeLevel < _fadeTarget)
        {
            _fadeLevel = Math.Min(_fadeTarget, _fadeLevel + FadeStep);
            Opacity = Motion.Ease((float)_fadeLevel);

            if (_fadeLevel >= _fadeTarget)
                _fadeTimer.Stop();

            return;
        }

        _fadeLevel = Math.Max(_fadeTarget, _fadeLevel - FadeStep);
        Opacity = Motion.Ease((float)_fadeLevel);

        if (_fadeLevel > _fadeTarget)
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
    // is logged and said in the header, and the others still work. Off in
    // Settings, none is taken.
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
        _idleTimer.Stop();
        _idleSwitcher.Restore(); // Whoever closes the app is at the keyboard.

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
        // The sidebar and a page beside it; the height follows the pages (see ResizeToFitPages).
        ClientSize = new Size(Sidebar.SidebarWidth + Pages.PageView.PageWidth, S(600));
        Text = "RazerHelper";
        StartPosition = FormStartPosition.Manual;

        // The rounded corners and the outline come from Windows (see
        // OnHandleCreated), as on the Settings and Battery windows.
    }

    private void BuildView()
    {
        var performancePage = new Pages.PerformancePage(_performanceSection, _fanSection, _batterySection);

        var displayPage = new Pages.DisplayPage(_displaySection, _lightingSection, _settings.KeyboardOffWithScreen);
        displayPage.KeyboardOffWithScreenChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { KeyboardOffWithScreen = enabled });
            _keyboardScreenOff.SetEnabled(enabled);
        };

        var powerPage = new Pages.PowerPage(
            BatteryReader.Read,
            _performanceSection,
            _settings.AutoSwitchProfiles,
            (_settings.IdleSwitch, _settings.IdleMinutes, _settings.IdlePlan),
            ListPowerPlans())
        {
            ListPlans = ListPowerPlans
        };
        powerPage.AutoSwitchProfilesChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { AutoSwitchProfiles = enabled });
            _performanceSection.AutoSwitchProfiles = enabled;
        };
        powerPage.IdleChanged += (_, choice) =>
        {
            SaveSettings(_settings with { IdleSwitch = choice.Enabled, IdleMinutes = choice.Minutes, IdlePlan = choice.Plan });
            _idleSwitcher.Configure(choice.Enabled, choice.Minutes, choice.Plan);
        };

        // Free up memory, temporary files and the GPU, each on its own button.
        _optimizePage = new Pages.OptimizePage(
            new MemoryTrimmer(new WindowsProcessMemory()),
            TempCleaner.ForCurrentUser(),
            _servicesSection,
            _settings.CloseGpuAppsOnUnplug,
            new Pages.OptimizePage.MemoryCache(WindowsMemoryCache.CachedBytes, () => ElevatedRunner.RunAsync(MemoryCacheCommand.Arguments())));
        _optimizePage.FreeUpGpuRequested += async (_, _) => await FreeUpGpuAsync();
        _optimizePage.CloseGpuAppsOnUnplugChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { CloseGpuAppsOnUnplug = enabled });
            _dgpuCoordinator.Enabled = enabled;
        };

        _pages[DashboardPage.Performance] = performancePage;
        _pages[DashboardPage.Display] = displayPage;
        _pages[DashboardPage.Power] = powerPage;
        _pages[DashboardPage.System] = new Pages.SystemPage(SystemInfoReader.Read);
        _pages[DashboardPage.Optimize] = _optimizePage;
        _pages[DashboardPage.Settings] = CreateSettingsPage();

        foreach (var page in _pages.Values)
        {
            page.HoldOpen = KeepOpen;
            page.Location = Point.Empty;
            page.Visible = false;

            // A page that grows (Razer's software found, an option switched
            // on) may need a taller window.
            page.SizeChanged += (_, _) => ResizeToFitPages();
            _pageHost.Controls.Add(page);
        }

        _pages[_currentPage].Visible = true;
        _sidebar.Select(_currentPage);
        _sidebar.PageRequested += (_, page) => ShowPage(page);
        _sidebar.CloseRequested += (_, _) => RequestHide();

        // The battery's More details goes to its section.
        _batterySection.DetailsRequested += (_, _) => ShowPage(DashboardPage.Power);

        // Dock order: the sidebar docks first, and the page fills what is left.
        Controls.Add(_pageHost);
        Controls.Add(_sidebar);

        ResizeToFitPages();
    }

    // The plans for the Idle option, read as Battery and power comes on screen.
    private IReadOnlyList<PowerPlan> ListPowerPlans()
    {
        try
        {
            return _powerPlans.List();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            AppLog.Error("Could not list the power plans.", exception);
            return [];
        }
    }

    /// <summary>Puts a section on show and lights its entry; the one before is told it has left.</summary>
    private void ShowPage(DashboardPage page)
    {
        if (page == _currentPage)
            return;

        var leaving = _pages[_currentPage];
        leaving.OnPageHidden();

        _currentPage = page;
        _sidebar.Select(page);

        var showing = _pages[page];
        _pageHost.AutoScrollPosition = Point.Empty;
        showing.Visible = true;
        leaving.Visible = false;

        if (Visible)
            showing.OnPageShown();
    }

    // The window is as tall as its tallest section, so it keeps one size
    // whichever is on show, and no taller than the screen (the page scrolls
    // then). Re-anchored to the taskbar so it does not end up floating or
    // overlapping it.
    private void ResizeToFitPages()
    {
        var tallest = _pages.Values.Max(page => page.GetPreferredSize(Size.Empty).Height);
        var screen = Screen.FromPoint(Visible ? Location : Cursor.Position).WorkingArea;
        var height = Math.Min(Math.Max(tallest, S(560)), screen.Height - S(16));
        var size = new Size(Sidebar.SidebarWidth + Pages.PageView.PageWidth, height);

        if (ClientSize == size)
            return;

        ClientSize = size;

        if (Visible)
            Location = TaskbarPlacement.GetPopupLocation(Size);
    }

    // The app's logo, always in its own green whatever the mode, drawn from the
    // icon file's largest size down to <paramref name="size"/> so it stays smooth.
    private static Bitmap? LoadLogo(int size)
    {
        try
        {
            using var stream = typeof(TrayPopupForm).Assembly.GetManifestResourceStream("RazerHelper.ico");

            if (stream is null)
                return null;

            using var icon = new Icon(stream, 256, 256);
            using var large = icon.ToBitmap();
            var logo = new Bitmap(size, size);

            using var graphics = Graphics.FromImage(logo);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(large, 0, 0, size, size);
            return logo;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            AppLog.Error("Could not load the logo.", exception);
            return null;
        }
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

    /// <summary>
    /// Opens a window beside the popup and in front of it, without blocking
    /// the popup. Only one is open at a time: another one closes first, and
    /// asking for the one already open brings it to the front.
    /// </summary>
    private void ShowBeside<T>(Func<T> create, Action<T>? whenClosed = null) where T : Form
    {
        if (_sideWindow is T open)
        {
            open.Activate();
            return;
        }

        _sideWindow?.Close();

        var window = create();
        window.TopMost = TopMost;
        _sideWindow = window;

        // The popup stays open while the window is, even when it has the focus.
        var hold = KeepOpen();

        var closed = false;

        window.FormClosed += (_, _) =>
        {
            // Closing the popup closes the windows it owns, which can report
            // closing a second time; act on the first only.
            if (closed)
                return;

            closed = true;
            hold.Dispose();

            if (_sideWindow == window)
                _sideWindow = null;

            // After the window has finished closing: what follows (a restart
            // in a new language, a reset) may close the popup too.
            if (whenClosed is not null && !IsDisposed)
                BeginInvoke(() => whenClosed(window));
        };

        window.Show(this);
    }

    // Custom's CPU and GPU levels. It closes by itself if the laptop leaves
    // Custom meanwhile.
    private void ShowCustomBoost()
    {
        // The fan poll already reads the CPU temperature for the header; pass it on too.
        void ShowTemperature(object? sender, TemperatureReading reading) =>
            (_sideWindow as CustomBoostForm)?.ShowCpuTemperature(reading.CpuCelsius);

        ShowBeside(
            () =>
            {
                var boostForm = new CustomBoostForm(_performanceSection.BoostSelectors);
                boostForm.PlaceBeside(this);
                _fanSection.TemperaturesRead += ShowTemperature;
                return boostForm;
            },
            _ => _fanSection.TemperaturesRead -= ShowTemperature);
    }

    private Pages.SettingsPage CreateSettingsPage()
    {
        var settingsPage = new Pages.SettingsPage(_settings, _startupRegistration);

        settingsPage.HideWhenClickedAwayChanged += (_, enabled) =>
            SaveSettings(_settings with { HideWhenClickedAway = enabled });

        settingsPage.AlwaysOnTopChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { AlwaysOnTop = enabled });
            TopMost = enabled;
        };

        settingsPage.ProfileShortcutsChanged += (_, enabled) =>
        {
            SaveSettings(_settings with { ProfileShortcuts = enabled });

            if (enabled)
            {
                StartProfileShortcuts();
                return;
            }

            _profileShortcuts.Unregister();
            AppLog.Info("Performance mode shortcuts are off.");
        };

        // Once the click that asked for it has finished: a restart or a reset closes the window.
        settingsPage.ResetConfirmed += (_, _) => BeginInvoke(() => _ = ResetToDefaultsAsync());
        settingsPage.LanguageChosen += (_, language) => BeginInvoke(() => RestartInLanguage(language));

        return settingsPage;
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
        // Before the settings holding it are cleared: the CPU boost Silent took away comes back.
        _silentTurbo.Restore();
        _idleSwitcher.Restore();

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

    // Successes are visible in the controls themselves, so only failures are
    // worth words. They show in red at the foot of the sidebar, in the
    // laptop's place, until the next result.
    private void ShowStatus(SectionStatus status) =>
        _sidebar.ShowStatus(status.IsError ? status.Message : _modelName, status.IsError);

    private void CheckForSupportedDevice()
    {
        if (_model is RazerLaptopModel model)
        {
            _modelName = model.Name;
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

    // An earlier version's Silent also leaned the CPU's energy preference
    // towards efficiency. Silent no longer does, so what it replaced is put
    // back once, whatever mode the laptop is in.
    private void GiveBackEnergyPreference(IPowerPlanValue preference)
    {
        if (_settings.CpuEfficiencyBeforeSilent is not { } saved)
            return;

        try
        {
            preference.Write(saved.Scheme, saved.PluggedIn, saved.OnBattery);
            AppLog.Info($"CPU energy preference given back ({saved.PluggedIn} plugged in, {saved.OnBattery} on battery): Silent no longer changes it.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not give the CPU energy preference back.", exception);
        }

        SaveSettings(_settings with { CpuEfficiencyBeforeSilent = null });
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
        _silentTurbo.OnModeChanged(state.Mode);

        // The levels only mean something in Custom (the charger switching
        // profiles, or the Fn keys, can leave it while the window is open).
        if (state.Mode is PerformanceMode known && known != PerformanceMode.Custom)
            (_sideWindow as CustomBoostForm)?.Close();
    }

    private void ServicesSection_HasServicesChanged(object? sender, bool hasServices)
    {
        _optimizePage.ShowServices(hasServices);
    }

    // A dialog takes focus from the popup, which would hide it (and the dialog
    // with it) unless auto-hide is held off. Hold it for as long as the
    // returned value is not disposed.
    private OpenHold KeepOpen() => new(this);

    // No power plans and nobody ever away, for tests and previews.
    private sealed class NoPowerPlans : IPowerPlans
    {
        public IReadOnlyList<PowerPlan> List() => [];

        public Guid Active() => Guid.Empty;

        public void Activate(Guid plan)
        {
        }
    }

    private sealed class NeverIdle : IIdleClock
    {
        public TimeSpan SinceLastInput() => TimeSpan.Zero;

        public bool ScreenKeptOn() => false;
    }

    // No power plan to change, for tests and previews.
    private sealed class NoPowerPlan : IPowerPlanValue
    {
        public SavedPlanValue Read() => new(Guid.Empty, 0, 0);

        public void Write(Guid scheme, uint pluggedIn, uint onBattery)
        {
        }
    }

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
        // The sidebar and the page reach the edges, so the outline is Windows' own.
        WindowChrome.Apply(Handle, DividerColor);
    }

    protected override void Dispose(bool disposing)
    {
        // Sections unsubscribe from the shared services as they are disposed,
        // so those must go first; then the form releases what it created.
        base.Dispose(disposing);

        if (disposing)
        {
            _hotkey.Dispose();
            _profileShortcuts.Dispose();
            _profileToast.Dispose();
            _displayWatcher.Dispose();
            _fadeTimer.Dispose();
            _idleTimer.Dispose();
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

            // A window beside the popup goes with it.
            _sideWindow?.Close();

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
            _lightingSection.StartWatchingBrightness();

            // Razer's services can be started or stopped from outside the app.
            _ = _servicesSection.RefreshAsync();

            _pages[_currentPage].OnPageShown();
        }
        else
        {
            _pages[_currentPage].OnPageHidden();
            _fanSection.StopPolling();
            _lightingSection.StopWatchingBrightness();
        }
    }
}
