using System.Globalization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// Everything that frees up the laptop: memory, old temporary files, the
/// NVIDIA shader cache and the dedicated GPU, each on its own button; the
/// offer to free up the GPU on unplugging; the hibernation file; and, when it
/// is installed, Razer's own software. Nothing that is running is closed
/// without asking.
/// </summary>
internal sealed class OptimizePage : PageView
{
    private readonly MemoryTrimmer _trimmer;
    private readonly BusySlot _memorySlot;
    private readonly Label _memoryHint;
    private readonly MemoryCache? _cache;
    private readonly CleanRow _temp;
    private readonly CleanRow _shaders;
    private readonly ToggleSwitch _closeGpuApps;
    private readonly Hibernation? _hibernation;
    private readonly ToggleSwitch _hibernationSwitch;
    private readonly Label _hibernationHint;
    private readonly Control _servicesDivider;
    private readonly Panel _services;
    private bool _settingHibernation;

    /// <summary>Windows' memory cache: how big it is, and clearing it (with administrator rights).</summary>
    internal sealed record MemoryCache(Func<long?> CachedBytes, Func<Task<ElevatedResult>> ClearAsync);

    /// <summary>Windows' hibernation: whether it is on, its file's size, and turning it off or on (with administrator rights).</summary>
    internal sealed record Hibernation(Func<bool?> IsEnabled, Func<long?> FileBytes, Func<bool, Task<ElevatedResult>> SetAsync);

    /// <param name="cache">Left out (previews), Free up memory does not touch the cache.</param>
    /// <param name="hibernation">Left out (previews), the hibernation row shows it on and changes nothing.</param>
    public OptimizePage(
        MemoryTrimmer trimmer,
        TempCleaner cleaner,
        TempCleaner shaderCache,
        ServicesSection services,
        bool closeGpuAppsOnUnplug,
        MemoryCache? cache = null,
        Hibernation? hibernation = null)
    {
        _trimmer = trimmer;
        _cache = cache;
        _hibernation = hibernation;

        _memorySlot = new BusySlot(CreateRowButton("Free up"));
        _memorySlot.Button.Click += async (_, _) => await FreeUpMemoryAsync();
        var memory = CreateCard("Free up memory", "Frees unused RAM and clears Windows' cache (asks for permission).", _memorySlot);
        _memoryHint = HintOf(memory);
        // 6px lower, so its text starts where the cards' does on Battery and System.
        memory.Margin = new Padding(0, S(6), 0, 0);
        Add(memory);

        _temp = AddCleanRow(cleaner, "Temporary files", "Clean", new CleanTexts(
            Name: "temporary files",
            Nothing: "Nothing to clean: no temporary files over a day old.",
            Found: "{0} in {1} files over a day old.",
            Working: "Cleaning...",
            CouldNotRead: "Could not read the temporary folder.",
            CouldNotClean: "Could not clean the temporary files.",
            Missing: "Could not read the temporary folder."));

        _shaders = AddCleanRow(shaderCache, "NVIDIA shader cache", "Clear", new CleanTexts(
            Name: "NVIDIA shader cache",
            Nothing: "Empty. Games rebuild it as they load.",
            Found: "{0} in {1} files. Clearing it can fix stutter after a driver update.",
            Working: "Clearing...",
            CouldNotRead: "Could not read the shader cache.",
            CouldNotClean: "Could not clear the shader cache.",
            Missing: "No NVIDIA shader cache on this PC."));

        var gpuSlot = new BusySlot(CreateRowButton("Free up"));
        gpuSlot.Button.Click += async (_, _) =>
        {
            if (FreeUpGpu is not { } freeUp)
                return;

            gpuSlot.Busy = true;

            try
            {
                await freeUp();
            }
            finally
            {
                if (!IsDisposed)
                    gpuSlot.Busy = false;
            }
        };
        Add(CreateDivider());
        Add(CreateCard("Free up GPU", "Closes the apps keeping the dedicated GPU awake, if you confirm.", gpuSlot));

        var unplugged = CreateSwitchCard("Free up GPU when unplugged", "Offers to close apps using the dedicated GPU, to save battery.", out _closeGpuApps);
        Add(CreateDivider());
        Add(unplugged);
        _closeGpuApps.Checked = closeGpuAppsOnUnplug;
        _closeGpuApps.CheckedChanged += (_, _) => CloseGpuAppsOnUnplugChanged?.Invoke(this, _closeGpuApps.Checked);

        var hibernationCard = CreateSwitchCard("Hibernation", " ", out _hibernationSwitch);
        _hibernationHint = HintOf(hibernationCard);
        Add(CreateDivider());
        Add(hibernationCard);
        ShowHibernation();
        _hibernationSwitch.CheckedChanged += async (_, _) => await SetHibernationAsync(_hibernationSwitch.Checked);

        // Razer's software, only once it is known to be installed.
        _servicesDivider = CreateDivider();
        _servicesDivider.Visible = false;
        _services = new Panel
        {
            BackColor = BackgroundColor,
            Margin = new Padding(0, S(16), 0, 0),
            Size = new Size(ContentWidth, ServicesSection.RowHeight - SectionPanel.GapBelow),
            Visible = false
        };
        services.Margin = Padding.Empty;
        _services.Controls.Add(services);
        Add(_servicesDivider);
        Add(_services);
    }

    /// <summary>What the GPU row's button runs (the window's Free up GPU); the row shows a spinner until it ends.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<Task>? FreeUpGpu { get; set; }

    public event EventHandler<bool>? CloseGpuAppsOnUnplugChanged;

    /// <summary>Shows or hides the Razer software row, as the section learns whether it is installed.</summary>
    public void ShowServices(bool shown)
    {
        _servicesDivider.Visible = shown;
        _services.Visible = shown;
    }

    // The folders fill up between visits, and hibernation may be changed
    // outside the app; all of it is read again each time.
    public override async void OnPageShown()
    {
        if (!_settingHibernation)
            ShowHibernation();

        await Task.WhenAll(ScanAsync(_temp), ScanAsync(_shaders));
    }

    private static Button CreateRowButton(string text)
    {
        var button = CreateSmallButton(text);
        button.Anchor = AnchorStyles.Right;
        button.Height = S(38);
        button.Width = Math.Max(button.Width + S(16), S(96));
        return button;
    }

    private async Task FreeUpMemoryAsync()
    {
        _memorySlot.Busy = true;
        _memoryHint.Text = L.T("Freeing up memory...");

        try
        {
            var result = await Task.Run(_trimmer.Trim);
            AppLog.Info($"Free up memory: {result.Programs} programs trimmed, {result.FreedBytes} bytes freed.");
            var trimmed = L.F("{0} freed from {1} programs in the background.", FormatSize(result.FreedBytes), result.Programs);
            _memoryHint.Text = trimmed;

            if (_cache is not null)
            {
                // Windows' permission prompt takes the focus from the window.
                using var hold = KeepOpen();
                _memoryHint.Text = trimmed + " " + await ClearCacheAsync(_cache);
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not free up memory.", exception);
            _memoryHint.Text = L.T("Could not free up memory.");
        }
        finally
        {
            if (!IsDisposed)
                _memorySlot.Busy = false;
        }
    }

    // The cache's part: Windows asks for administrator permission, the
    // elevated helper clears it, and the line says how much went. Declining
    // the prompt only leaves the cache as it was.
    private static async Task<string> ClearCacheAsync(MemoryCache cache)
    {
        var before = await Task.Run(cache.CachedBytes);
        var result = await cache.ClearAsync();

        if (result.UserDeclined)
            return L.T("The memory cache was kept: administrator permission was not given.");

        if (result.ExitCode != MemoryCacheCommand.Success)
            return L.T("The memory cache could not be cleared.");

        var after = await Task.Run(cache.CachedBytes);
        var cleared = before is { } b && after is { } a ? Math.Max(0, b - a) : 0;
        AppLog.Info($"Free up memory: {cleared} bytes of memory cache cleared.");
        return L.F("{0} of memory cache cleared.", FormatSize(cleared));
    }

    // --- Folders to clean: temporary files and the shader cache ------------

    private sealed record CleanTexts(string Name, string Nothing, string Found, string Working, string CouldNotRead, string CouldNotClean, string Missing);

    private sealed class CleanRow(TempCleaner cleaner, BusySlot slot, Label hint, CleanTexts texts)
    {
        public TempCleaner Cleaner { get; } = cleaner;
        public BusySlot Slot { get; } = slot;
        public Label Hint { get; } = hint;
        public CleanTexts Texts { get; } = texts;
        public bool Scanning { get; set; }
    }

    private CleanRow AddCleanRow(TempCleaner cleaner, string title, string buttonText, CleanTexts texts)
    {
        var slot = new BusySlot(CreateRowButton(buttonText));
        slot.Button.Enabled = false; // Until the scan says there is something to clean.

        var card = CreateCard(title, "Scanning...", slot);
        var row = new CleanRow(cleaner, slot, HintOf(card), texts);
        slot.Button.Click += async (_, _) => await CleanAsync(row);

        Add(CreateDivider());
        Add(card);
        return row;
    }

    private async Task ScanAsync(CleanRow row)
    {
        if (row.Scanning || row.Slot.Busy)
            return;

        row.Scanning = true;

        try
        {
            var (exists, found) = await Task.Run(() => (row.Cleaner.AnyFolderExists, row.Cleaner.Scan()));

            if (IsDisposed)
                return;

            row.Hint.Text = !exists ? L.T(row.Texts.Missing)
                : found.Files == 0 ? L.T(row.Texts.Nothing)
                : L.F(row.Texts.Found, FormatSize(found.Bytes), found.Files.ToString("N0", CultureInfo.CurrentCulture));
            row.Slot.Button.Enabled = found.Files > 0;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not scan the {row.Texts.Name}.", exception);

            if (!IsDisposed)
                row.Hint.Text = L.T(row.Texts.CouldNotRead);
        }
        finally
        {
            row.Scanning = false;
        }
    }

    private async Task CleanAsync(CleanRow row)
    {
        // Nothing is left to clean afterwards, so the button stays off until the next scan.
        row.Slot.Button.Enabled = false;
        row.Slot.Busy = true;
        row.Hint.Text = L.T(row.Texts.Working);

        try
        {
            var result = await Task.Run(row.Cleaner.Clean);
            AppLog.Info($"{row.Texts.Name}: {result.Files} removed ({result.Bytes} bytes), {result.Skipped} in use left.");

            if (IsDisposed)
                return;

            var cleaned = L.F("{0} freed ({1} files).", FormatSize(result.Bytes), result.Files.ToString("N0", CultureInfo.CurrentCulture));
            row.Hint.Text = result.Skipped == 0
                ? cleaned
                : $"{cleaned} {L.F("{0} in use were left.", result.Skipped.ToString("N0", CultureInfo.CurrentCulture))}";
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not clean the {row.Texts.Name}.", exception);

            if (!IsDisposed)
                row.Hint.Text = L.T(row.Texts.CouldNotClean);
        }
        finally
        {
            if (!IsDisposed)
                row.Slot.Busy = false;
        }
    }

    // --- Hibernation ----------------------------------------------------------

    // The switch is on while hibernation is; the line says what its file takes.
    private void ShowHibernation()
    {
        var enabled = _hibernation?.IsEnabled() ?? true;

        _settingHibernation = true;
        _hibernationSwitch.Checked = enabled;
        _settingHibernation = false;

        _hibernationHint.Text = !enabled
            ? L.T("Off, with no file on disk. Hibernate and fast startup are off too.")
            : _hibernation?.FileBytes() is { } bytes
                ? L.F("Its file takes {0}. Off frees it, with no hibernate or fast startup.", FormatSize(bytes))
                : L.T("Off frees its file's disk space, but turns off hibernate and fast startup.");
    }

    // Windows asks for administrator permission; declining, or a refusal,
    // leaves hibernation as it was and puts the switch back.
    private async Task SetHibernationAsync(bool enabled)
    {
        if (_settingHibernation || _hibernation is null)
            return;

        _settingHibernation = true;
        _hibernationSwitch.Enabled = false;
        _hibernationHint.Text = L.T("Asking Windows (asks for permission)...");

        string? problem = null;

        try
        {
            // Windows' permission prompt takes the focus from the window.
            using var hold = KeepOpen();
            var result = await _hibernation.SetAsync(enabled);

            if (result.UserDeclined)
                problem = L.T("Kept as it was: administrator permission was not given.");
            else if (result.ExitCode != HibernationCommand.Success)
                problem = L.T("Windows did not make the change.");
            else
                AppLog.Info($"Hibernation turned {(enabled ? "on" : "off")}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not change hibernation.", exception);
            problem = L.T("Windows did not make the change.");
        }

        if (IsDisposed)
            return;

        _settingHibernation = false;
        _hibernationSwitch.Enabled = true;
        ShowHibernation();

        if (problem is not null)
            _hibernationHint.Text = problem;
    }

    private static string FormatSize(long bytes) => SystemInfoText.Size(bytes, CultureInfo.CurrentCulture);

    // A row's button, which gives its place to the turning circle (the one
    // System shows while it reads) for as long as its job runs.
    private sealed class BusySlot : Panel
    {
        private readonly LoadingSpinner _spinner = new() { Diameter = 28, Dock = DockStyle.Fill, Visible = false };

        public BusySlot(Button button)
        {
            Button = button;
            Anchor = button.Anchor;
            BackColor = BackgroundColor;
            Size = button.Size;

            button.Dock = DockStyle.Fill;
            Controls.Add(button);
            Controls.Add(_spinner);
        }

        public Button Button { get; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Busy
        {
            get => _spinner.Visible;
            set
            {
                _spinner.Visible = value;
                Button.Visible = !value;
            }
        }
    }
}
