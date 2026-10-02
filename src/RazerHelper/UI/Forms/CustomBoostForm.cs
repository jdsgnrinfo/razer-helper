using System.Globalization;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Custom mode's window beside the popup, like Settings: for the CPU and
/// then the GPU, its boost levels and, under them, what it is doing now. Each
/// click applies at once, so there is only a close X (Esc works too). The
/// selectors belong to the Performance section, which keeps them up to date;
/// this window borrows them and hands them back. The figures are read every
/// couple of seconds, only while the window is open.
/// </summary>
internal sealed class CustomBoostForm : Form
{
    private const int RefreshIntervalMilliseconds = 2_000;

    private static int ContentWidth => S(512);

    // A row of figures: 16px above, then each figure's name and, under it, its value.
    private static int StatsHeight => S(16 + 22 + 22);

    // Four columns in every row of figures, so the CPU's three line up with the GPU's four.
    private const int StatColumns = 4;

    private readonly CustomBoostSelectors _selectors;
    private readonly CpuStatsReader _cpuReader = new();
    private readonly GpuStatsReader _gpuReader = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };

    private readonly Label _cpuTemperature;
    private readonly Label _cpuUsage;
    private readonly Label _cpuSpeed;
    private readonly Label _gpuTemperature;
    private readonly Label _gpuUsage;
    private readonly Label _gpuCoreClock;
    private readonly Label _gpuMemoryClock;

    private Form? _anchor;
    private Task _read = Task.CompletedTask;

    public CustomBoostForm(CustomBoostSelectors selectors)
    {
        _selectors = selectors;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = DesignFont(12);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = $"RazerHelper {L.T("Custom")}";

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

        layout.Controls.Add(WindowTitleRow.Create(this, "Custom", ContentWidth));

        var cpuValues = CreateStatsRow(out var cpuStats, "Temperature", "Usage", "Speed");
        (_cpuTemperature, _cpuUsage, _cpuSpeed) = (cpuValues[0], cpuValues[1], cpuValues[2]);

        var gpuValues = CreateStatsRow(out var gpuStats, "Temperature", "Usage", "Core clock", "Memory clock");
        (_gpuTemperature, _gpuUsage, _gpuCoreClock, _gpuMemoryClock) = (gpuValues[0], gpuValues[1], gpuValues[2], gpuValues[3]);

        // Each chip: its selector, then its figures; a line between the two chips.
        AddSelector(layout, _selectors.Cpu, S(20));
        layout.Controls.Add(cpuStats);
        layout.Controls.Add(new Panel
        {
            BackColor = DividerColor,
            Height = S(1),
            Margin = new Padding(0, S(26), 0, 0),
            Width = ContentWidth
        });
        AddSelector(layout, _selectors.Gpu, S(18));
        layout.Controls.Add(gpuStats);

        layout.Controls.Add(new InfoNote(L.T("Boost levels apply right away and are saved in the current power profile."), ContentWidth)
        {
            Margin = new Padding(0, S(24), 0, 0)
        });

        WindowOutline.Attach(layout);
        WindowFade.Attach(this);
        Controls.Add(layout);

        _refreshTimer.Tick += (_, _) => StartRead();
    }

    /// <summary>
    /// The CPU temperature, from the laptop's controller. It is already read
    /// for the popup, so the host passes each reading on rather than this
    /// window asking the controller again.
    /// </summary>
    public void ShowCpuTemperature(double? celsius) =>
        _cpuTemperature.Text = HardwareStatsText.Celsius(celsius);

    /// <summary>Opens next to the popup instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // The CPU's first read only primes its counters; the figures come with the next.
        StartRead();
        _refreshTimer.Start();

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
            Close();
    }

    // Hands the selectors back before the window is disposed, which would
    // otherwise dispose them with it.
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _selectors.Detach();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();

            // A read may still be running on another thread; let it finish first.
            _read.ContinueWith(_ =>
            {
                _cpuReader.Dispose();
                _gpuReader.Dispose();
            }, TaskScheduler.Default);
        }

        base.Dispose(disposing);
    }

    private static void AddSelector(FlowLayoutPanel layout, Panel selector, int gapAbove)
    {
        selector.Dock = DockStyle.None;
        selector.Margin = new Padding(0, gapAbove, 0, 0);
        selector.Width = ContentWidth;
        layout.Controls.Add(selector);
    }

    // Reads off the UI thread (the counters and the driver can take a moment),
    // one read at a time, then shows the figures if the window is still open.
    private void StartRead()
    {
        if (!_read.IsCompleted)
            return;

        _read = Task.Run(() => (Cpu: _cpuReader.Read(), Gpu: _gpuReader.Read()))
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && !IsDisposed)
                    ShowStats(task.Result.Cpu, task.Result.Gpu);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ShowStats(CpuStats cpu, GpuStats gpu)
    {
        _cpuUsage.Text = HardwareStatsText.Percent(cpu.UsagePercent);
        _cpuSpeed.Text = HardwareStatsText.Gigahertz(cpu.CurrentMhz, CultureInfo.CurrentCulture);

        // A GPU that is powered down has nothing to show; saying so beats a row of dashes.
        var asleep = gpu.Asleep ? L.T("Asleep") : null;
        _gpuTemperature.Text = asleep ?? HardwareStatsText.Celsius(gpu.Celsius);
        _gpuUsage.Text = asleep ?? HardwareStatsText.Percent(gpu.UsagePercent);
        _gpuCoreClock.Text = asleep ?? HardwareStatsText.Megahertz(gpu.CoreMhz);
        _gpuMemoryClock.Text = asleep ?? HardwareStatsText.Megahertz(gpu.MemoryMhz);
    }

    // The figures side by side in equal columns: each one's name in white and,
    // under it, its value in grey. Returns the value labels in the order of the names.
    private static Label[] CreateStatsRow(out Control row, params string[] names)
    {
        var table = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = StatColumns,
            Margin = Padding.Empty,
            Padding = new Padding(0, S(16), 0, 0),
            RowCount = 2,
            Size = new Size(ContentWidth, StatsHeight)
        };

        for (var index = 0; index < StatColumns; index++)
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / StatColumns));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var values = new Label[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            var name = CreateStatLabel(SemiBoldFont(14));
            name.Text = L.T(names[index]);
            table.Controls.Add(name, index, 0);

            // The reading in grey under its white name, as in the design.
            values[index] = CreateStatLabel(DesignFont(14));
            values[index].ForeColor = SubtleTextColor;
            values[index].Text = HardwareStatsText.NoReading;
            table.Controls.Add(values[index], index, 1);
        }

        row = table;
        return values;
    }

    private static Label CreateStatLabel(Font font) => new()
    {
        AutoEllipsis = true,
        BackColor = BackgroundColor,
        Dock = DockStyle.Fill,
        Font = font,
        ForeColor = Color.White,
        Margin = Padding.Empty,
        TextAlign = ContentAlignment.TopLeft
    };
}
