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
/// Everything that frees up the laptop: memory, old temporary files and the
/// dedicated GPU, each on its own button; the offer to free up the GPU on
/// unplugging; and, when it is installed, Razer's own software. Nothing that
/// is running is closed without asking.
/// </summary>
internal sealed class OptimizePage : PageView
{
    private readonly MemoryTrimmer _trimmer;
    private readonly TempCleaner _cleaner;
    private readonly BusySlot _memorySlot;
    private readonly Label _memoryHint;
    private readonly BusySlot _tempSlot;
    private readonly Label _tempHint;
    private readonly MemoryCache? _cache;
    private readonly ToggleSwitch _closeGpuApps;
    private readonly Control _servicesDivider;
    private readonly Panel _services;
    private bool _scanning;

    /// <summary>Windows' memory cache: how big it is, and clearing it (with administrator rights).</summary>
    internal sealed record MemoryCache(Func<long?> CachedBytes, Func<Task<ElevatedResult>> ClearAsync);

    /// <param name="cache">Left out (previews), Free up memory does not touch the cache.</param>
    public OptimizePage(MemoryTrimmer trimmer, TempCleaner cleaner, ServicesSection services, bool closeGpuAppsOnUnplug, MemoryCache? cache = null)
    {
        _trimmer = trimmer;
        _cleaner = cleaner;
        _cache = cache;

        _memorySlot = new BusySlot(CreateRowButton("Free up"));
        _memorySlot.Button.Click += async (_, _) => await FreeUpMemoryAsync();
        var memory = CreateCard("Free up memory", "Frees unused RAM and clears Windows' cache (asks for permission).", _memorySlot);
        _memoryHint = HintOf(memory);
        // 6px lower, so its text starts where the cards' does on Battery and System.
        memory.Margin = new Padding(0, S(6), 0, 0);
        Add(memory);

        _tempSlot = new BusySlot(CreateRowButton("Clean"));
        _tempSlot.Button.Enabled = false; // Until the scan says there is something to clean.
        _tempSlot.Button.Click += async (_, _) => await CleanTempFilesAsync();
        var temp = CreateCard("Temporary files", "Scanning...", _tempSlot);
        _tempHint = HintOf(temp);
        Add(CreateDivider());
        Add(temp);

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

    // The temporary folder fills up between visits; it is counted again each time.
    public override async void OnPageShown() => await ScanTempFilesAsync();

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

    private async Task ScanTempFilesAsync()
    {
        if (_scanning)
            return;

        _scanning = true;

        try
        {
            var found = await Task.Run(_cleaner.Scan);

            if (IsDisposed)
                return;

            _tempHint.Text = found.Files == 0
                ? L.T("Nothing to clean: no temporary files over a day old.")
                : L.F("{0} in {1} files over a day old.", FormatSize(found.Bytes), found.Files.ToString("N0", CultureInfo.CurrentCulture));
            _tempSlot.Button.Enabled = found.Files > 0;
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not scan the temporary files.", exception);

            if (!IsDisposed)
                _tempHint.Text = L.T("Could not read the temporary folder.");
        }
        finally
        {
            _scanning = false;
        }
    }

    private async Task CleanTempFilesAsync()
    {
        // Nothing is left to clean afterwards, so the button stays off until the next scan.
        _tempSlot.Button.Enabled = false;
        _tempSlot.Busy = true;
        _tempHint.Text = L.T("Cleaning...");

        try
        {
            var result = await Task.Run(_cleaner.Clean);
            AppLog.Info($"Temporary files: {result.Files} removed ({result.Bytes} bytes), {result.Skipped} in use left.");

            if (IsDisposed)
                return;

            var cleaned = L.F("{0} freed ({1} files).", FormatSize(result.Bytes), result.Files.ToString("N0", CultureInfo.CurrentCulture));
            _tempHint.Text = result.Skipped == 0
                ? cleaned
                : $"{cleaned} {L.F("{0} in use were left.", result.Skipped.ToString("N0", CultureInfo.CurrentCulture))}";
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not clean the temporary files.", exception);

            if (!IsDisposed)
                _tempHint.Text = L.T("Could not clean the temporary files.");
        }
        finally
        {
            if (!IsDisposed)
                _tempSlot.Busy = false;
        }
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
