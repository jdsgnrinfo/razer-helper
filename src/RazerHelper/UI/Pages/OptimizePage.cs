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
    private readonly Button _memoryButton;
    private readonly Label _memoryHint;
    private readonly Button _tempButton;
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

        _memoryButton = CreateRowButton("Free up");
        _memoryButton.Click += async (_, _) => await FreeUpMemoryAsync();
        var memory = CreateCard("Free up memory", "Moves what background programs are not using out of RAM and clears Windows' memory cache (asks for administrator permission). The game in front is left alone.", _memoryButton);
        _memoryHint = HintOf(memory);
        Add(memory);

        _tempButton = CreateRowButton("Clean");
        _tempButton.Enabled = false; // Until the scan says there is something to clean.
        _tempButton.Click += async (_, _) => await CleanTempFilesAsync();
        var temp = CreateCard("Temporary files", "Scanning...", _tempButton);
        _tempHint = HintOf(temp);
        Add(CreateDivider());
        Add(temp);

        var gpuButton = CreateRowButton("Free up");
        gpuButton.Click += (_, _) => FreeUpGpuRequested?.Invoke(this, EventArgs.Empty);
        Add(CreateDivider());
        Add(CreateCard("Free up GPU", "Lists apps keeping the dedicated GPU awake, and closes them only if you say so.", gpuButton));

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

        Add(new InfoNote(L.T("Nothing running is closed: memory is only moved out of RAM, and temporary files in use or under a day old stay."), ContentWidth)
        {
            Margin = new Padding(0, S(16), 0, 0)
        });
    }

    /// <summary>Raised by the GPU row's button; the window runs its Free up GPU.</summary>
    public event EventHandler? FreeUpGpuRequested;

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
        _memoryButton.Enabled = false;
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
                _memoryButton.Enabled = true;
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
            _tempButton.Enabled = found.Files > 0;
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
        _tempButton.Enabled = false;
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
    }

    private static string FormatSize(long bytes) => SystemInfoText.Size(bytes, CultureInfo.CurrentCulture);
}
