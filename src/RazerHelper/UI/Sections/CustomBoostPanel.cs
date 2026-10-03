using System.Globalization;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.UI.Pages;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Custom mode's levels, opened under the mode buttons while the laptop is in
/// Custom: the CPU on the left and the GPU on the right, each its boost
/// levels and, under them, what the chip is doing now. Each click applies at
/// once. The selectors belong to the Performance section, which keeps them up
/// to date; the figures are read every couple of seconds, only while they are
/// on screen (see <see cref="Start"/>).
/// </summary>
internal sealed class CustomBoostPanel : TableLayoutPanel
{
    private const int RefreshIntervalMilliseconds = 2_000;

    private static int ColumnGap => S(24);

    private static int ColumnWidth => (PageView.ContentWidth - ColumnGap) / 2;

    // Two figures to a row, each its name over its value; two rows.
    private static int StatsHeight => S(12 + 2 * (20 + 20) + 8);

    /// <summary>How tall the panel is.</summary>
    public static int PanelHeight => CustomBoostSelectors.SelectorHeight + StatsHeight;

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

    private Task _read = Task.CompletedTask;

    public CustomBoostPanel(CustomBoostSelectors selectors)
    {
        BackColor = BackgroundColor;
        ColumnCount = 2;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 1;
        Size = new Size(PageView.ContentWidth, PanelHeight);

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ColumnWidth + ColumnGap));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var cpuValues = CreateStats(out var cpuStats, "Temperature", "Usage", "Speed");
        (_cpuTemperature, _cpuUsage, _cpuSpeed) = (cpuValues[0], cpuValues[1], cpuValues[2]);

        var gpuValues = CreateStats(out var gpuStats, "Temperature", "Usage", "Core clock", "Memory clock");
        (_gpuTemperature, _gpuUsage, _gpuCoreClock, _gpuMemoryClock) = (gpuValues[0], gpuValues[1], gpuValues[2], gpuValues[3]);

        Controls.Add(CreateColumn(selectors.Cpu, cpuStats, ColumnGap), 0, 0);
        Controls.Add(CreateColumn(selectors.Gpu, gpuStats, 0), 1, 0);

        _refreshTimer.Tick += (_, _) => StartRead();
    }

    /// <summary>Reads the figures now and every couple of seconds.</summary>
    public void Start()
    {
        // The CPU's first read only primes its counters; the figures come with the next.
        StartRead();
        _refreshTimer.Start();
    }

    public void Stop() => _refreshTimer.Stop();

    /// <summary>
    /// The CPU temperature, from the laptop's controller. It is already read
    /// for the window, so the host passes each reading on rather than this
    /// panel asking the controller again.
    /// </summary>
    public void ShowCpuTemperature(double? celsius) =>
        _cpuTemperature.Text = HardwareStatsText.Celsius(celsius);

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

    // A chip's selector with its figures under it.
    private static Control CreateColumn(Panel selector, Control stats, int gapRight)
    {
        var column = new FlowLayoutPanel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 0, gapRight, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };

        selector.Dock = DockStyle.None;
        selector.Margin = Padding.Empty;
        selector.Width = ColumnWidth;
        column.Controls.Add(selector);
        column.Controls.Add(stats);
        return column;
    }

    // Reads off the UI thread (the counters and the driver can take a moment),
    // one read at a time, then shows the figures if the panel is still there.
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

    // The figures two to a row: each one's name in white and, under it, its
    // value in grey. Returns the value labels in the order of the names.
    private static Label[] CreateStats(out Control stats, params string[] names)
    {
        var table = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, S(12), 0, 0),
            RowCount = 4,
            Size = new Size(ColumnWidth, StatsHeight)
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, S(20)));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, S(20 + 8)));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, S(20)));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var values = new Label[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            var (column, row) = (index % 2, index / 2 * 2);

            var name = CreateStatLabel(SemiBoldFont(14));
            name.Text = L.T(names[index]);
            table.Controls.Add(name, column, row);

            values[index] = CreateStatLabel(DesignFont(14));
            values[index].ForeColor = SubtleTextColor;
            values[index].Text = HardwareStatsText.NoReading;
            table.Controls.Add(values[index], column, row + 1);
        }

        stats = table;
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
