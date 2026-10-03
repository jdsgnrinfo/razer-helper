using System.Globalization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Optimize window, opened from the green footer button: free up memory,
/// clear old temporary files, and free up the dedicated GPU. Each runs only
/// when its button is pressed, and none closes a program that is running.
/// </summary>
internal sealed class OptimizeForm : Form
{
    private static int ContentWidth => S(512);

    private readonly MemoryTrimmer _trimmer;
    private readonly TempCleaner _cleaner;
    private readonly Button _memoryButton;
    private readonly Label _memoryHint;
    private readonly Button _tempButton;
    private readonly Label _tempHint;
    private readonly MemoryCache? _cache;
    private Form? _anchor;

    /// <summary>Windows' memory cache: how big it is, and clearing it (with administrator rights).</summary>
    internal sealed record MemoryCache(Func<long?> CachedBytes, Func<Task<ElevatedResult>> ClearAsync);

    /// <param name="cache">Left out (previews), Free up memory does not touch the cache.</param>
    public OptimizeForm(MemoryTrimmer trimmer, TempCleaner cleaner, MemoryCache? cache = null)
    {
        _trimmer = trimmer;
        _cleaner = cleaner;
        _cache = cache;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true; // Esc closes.
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = L.T("RazerHelper Optimize");

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(24)),
            WrapContents = false
        };

        layout.Controls.Add(WindowTitleRow.Create(this, "Optimize", ContentWidth));

        _memoryButton = CreateRowButton("Free up");
        _memoryButton.Click += async (_, _) => await FreeUpMemoryAsync();
        var memory = SettingsForm.CreateCard("Free up memory", "Moves what background programs are not using out of RAM and clears Windows' memory cache (asks for administrator permission). The game in front is left alone.", _memoryButton);
        memory.Margin = new Padding(0, S(12), 0, 0);
        _memoryHint = HintOf(memory);
        layout.Controls.Add(memory);

        _tempButton = CreateRowButton("Clean");
        _tempButton.Enabled = false; // Until the scan says there is something to clean.
        _tempButton.Click += async (_, _) => await CleanTempFilesAsync();
        var temp = SettingsForm.CreateCard("Temporary files", "Scanning...", _tempButton);
        _tempHint = HintOf(temp);
        layout.Controls.Add(SettingsForm.CreateDivider());
        layout.Controls.Add(temp);

        var gpuButton = CreateRowButton("Free up");
        gpuButton.Click += (_, _) => FreeUpGpuRequested?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(SettingsForm.CreateDivider());
        layout.Controls.Add(SettingsForm.CreateCard("Free up GPU", "Lists apps keeping the dedicated GPU awake, and closes them only if you say so.", gpuButton));

        layout.Controls.Add(new InfoNote(L.T("Nothing running is closed: memory is only moved out of RAM, and temporary files in use or under a day old stay."), ContentWidth)
        {
            Margin = new Padding(0, S(16), 0, 0)
        });

        WindowOutline.Attach(layout);
        WindowFade.Attach(this);
        Controls.Add(layout);
    }

    /// <summary>Raised by the GPU row's button; the popup runs its Free up GPU.</summary>
    public event EventHandler? FreeUpGpuRequested;

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await ScanTempFilesAsync();
    }

    private static Button CreateRowButton(string text)
    {
        var button = CreateSmallButton(text);
        button.Anchor = AnchorStyles.Right;
        button.Height = S(38);
        button.Width = Math.Max(button.Width + S(16), S(96));
        return button;
    }

    // The grey line under a card's title.
    private static Label HintOf(TableLayoutPanel card) =>
        (Label)card.GetControlFromPosition(0, 0)!.Controls[1];

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
                _memoryHint.Text = trimmed + " " + await ClearCacheAsync(_cache);
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
            Close();
    }

    /// <summary>Opens next to the popup instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_anchor is null)
            return;

        // The size is only final once the layout has run.
        PerformLayout();
        var workingArea = Screen.FromRectangle(_anchor.Bounds).WorkingArea;
        Location = WindowPlacement.Beside(_anchor.Bounds, Size, workingArea);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, null); // The outline is drawn by WindowOutline instead.
    }
}
