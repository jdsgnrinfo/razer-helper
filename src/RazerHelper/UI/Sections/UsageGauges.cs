using System.Drawing.Drawing2D;
using System.Globalization;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.UI.Pages;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Three rings under the performance modes: how busy the CPU, the GPU and
/// the RAM are, each ring three quarters of a circle filling in green, with
/// two figures beside it (the CPU's and GPU's temperature and clock, the
/// RAM's speed and what is free). Read once a second, only while they are
/// on screen; between readings each ring and its number glide to the new
/// value, easing to a stop, rather than jump (see <see cref="Start"/>). The CPU temperature comes from the
/// fan poll, which already reads it (see <see cref="ShowCpuTemperature"/>).
/// </summary>
internal sealed class UsageGauges : Control
{
    private const int RefreshIntervalMilliseconds = 1_000;

    // How long a ring takes to glide to a new reading: most of the second
    // between readings, so it is always moving smoothly, never jumping.
    private const double GlideMilliseconds = 650;

    /// <summary>How tall the row of rings is.</summary>
    public static int GaugesHeight => S(76);

    private static int RingSize => S(76);
    private static float RingThickness => S(5.5f);
    private static int ColumnGap => S(16);
    private static int FiguresGap => S(12);

    private static readonly Color TrackColor = Color.FromArgb(0x26, 0x26, 0x26);
    private static readonly Color ArcStartColor = Color.FromArgb(0x2A, 0x8A, 0x1A);
    private static readonly Font PercentFont = DesignFont(16, FontStyle.Bold);
    private static readonly Font PercentSignFont = DesignFont(11, FontStyle.Bold);
    private static readonly Font RingLabelFont = SemiBoldFont(12.5f);
    private static readonly Font FigureLabelFont = DesignFont(12.5f);
    private static readonly Font FigureValueFont = SemiBoldFont(13);

    private readonly CpuStatsReader _cpuReader = new();
    private readonly GpuStatsReader _gpuReader = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };

    // Steps the rings towards their readings, about 60 times a second, only while one is moving.
    private readonly System.Windows.Forms.Timer _glideTimer = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Gauge[] _gauges;
    private Task _read = Task.CompletedTask;
    private int? _memoryMhz;
    private bool _memoryMhzRead;

    public UsageGauges()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = BackgroundColor;
        Margin = Padding.Empty;
        Size = new Size(PageView.ContentWidth, GaugesHeight);

        _gauges =
        [
            new Gauge("CPU", L.T("Temperature"), L.T("Frequency")),
            new Gauge("GPU", L.T("Temperature"), L.T("Frequency")),
            new Gauge("RAM", L.T("Speed"), L.T("Available"))
        ];

        _refreshTimer.Tick += (_, _) => StartRead();
        _glideTimer.Tick += (_, _) => Glide();
    }

    /// <summary>Reads now and once a second.</summary>
    public void Start()
    {
        // The CPU's first read only primes its counters; the figures come with the next.
        StartRead();
        _refreshTimer.Start();
    }

    public void Stop()
    {
        _refreshTimer.Stop();

        // Off screen: each ring settles on its reading, so it comes back still.
        _glideTimer.Stop();

        foreach (var gauge in _gauges)
            gauge.Shown = gauge.Percent;
    }

    /// <summary>The CPU temperature, from the laptop's controller.</summary>
    public void ShowCpuTemperature(double? celsius) => Show(_gauges[0], first: HardwareStatsText.Celsius(celsius));

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var columnWidth = (Width - 2 * ColumnGap) / 3f;

        for (var index = 0; index < _gauges.Length; index++)
            PaintGauge(graphics, _gauges[index], index * (columnWidth + ColumnGap), columnWidth);
    }

    private static void PaintGauge(Graphics graphics, Gauge gauge, float left, float width)
    {
        // The ring: three quarters of a circle, open at the bottom, filling clockwise.
        var inset = RingThickness / 2;
        var ring = new RectangleF(left + inset, inset, RingSize - RingThickness, RingSize - RingThickness);

        using (var track = new Pen(TrackColor, RingThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            graphics.DrawArc(track, ring, 135, 270);

        if (gauge.Shown is { } percent && percent > 0)
        {
            // The gradient spans the whole stroke, which reaches half its width
            // outside the ring; beyond its own bounds the brush would start
            // again, bright green over the dark start at the left.
            var span = RectangleF.Inflate(ring, RingThickness, RingThickness);
            using var brush = new LinearGradientBrush(span, ArcStartColor, RazerGreen, LinearGradientMode.Horizontal) { WrapMode = WrapMode.TileFlipX };
            using var arc = new Pen(brush, RingThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(arc, ring, 135, (float)Math.Max(0.5, 270 * Math.Min(percent, 100) / 100));
        }

        // Inside: the percentage, its sign smaller, and the part's name under it.
        var number = gauge.Shown is { } value ? Math.Round(value).ToString(CultureInfo.CurrentCulture) : HardwareStatsText.NoReading;
        var numberSize = TextRenderer.MeasureText(graphics, number, PercentFont, Size.Empty, TextFormatFlags.NoPadding);
        var signSize = gauge.Shown is null ? Size.Empty : TextRenderer.MeasureText(graphics, "%", PercentSignFont, Size.Empty, TextFormatFlags.NoPadding);
        var labelSize = TextRenderer.MeasureText(graphics, gauge.Name, RingLabelFont, Size.Empty, TextFormatFlags.NoPadding);

        var centerX = left + RingSize / 2f;
        var top = (RingSize - (numberSize.Height + labelSize.Height)) / 2f;
        var numberLeft = centerX - (numberSize.Width + signSize.Width) / 2f;

        TextRenderer.DrawText(graphics, number, PercentFont, Point.Round(new PointF(numberLeft, top)), Color.White, TextFormatFlags.NoPadding);

        if (gauge.Shown is not null)
        {
            // The sign sits on the number's baseline.
            var signTop = top + numberSize.Height - signSize.Height - S(1);
            TextRenderer.DrawText(graphics, "%", PercentSignFont, Point.Round(new PointF(numberLeft + numberSize.Width, signTop)), Color.White, TextFormatFlags.NoPadding);
        }

        TextRenderer.DrawText(graphics, gauge.Name, RingLabelFont, Point.Round(new PointF(centerX - labelSize.Width / 2f, top + numberSize.Height)), SubtleTextColor, TextFormatFlags.NoPadding);

        // Beside it: two figures, each its name in grey over its value.
        var figuresLeft = (int)(left + RingSize + FiguresGap);
        var figuresWidth = (int)(left + width) - figuresLeft;
        var labelHeight = TextRenderer.MeasureText(graphics, "Ag", FigureLabelFont, Size.Empty, TextFormatFlags.NoPadding).Height;
        var valueHeight = TextRenderer.MeasureText(graphics, "Ag", FigureValueFont, Size.Empty, TextFormatFlags.NoPadding).Height;
        var rowGap = S(6);
        var figuresTop = (RingSize - (2 * (labelHeight + valueHeight) + rowGap)) / 2;

        DrawFigure(graphics, gauge.FirstLabel, gauge.First, figuresLeft, figuresTop, figuresWidth, labelHeight, valueHeight);
        DrawFigure(graphics, gauge.SecondLabel, gauge.Second, figuresLeft, figuresTop + labelHeight + valueHeight + rowGap, figuresWidth, labelHeight, valueHeight);
    }

    private static void DrawFigure(Graphics graphics, string label, string value, int left, int top, int width, int labelHeight, int valueHeight)
    {
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        TextRenderer.DrawText(graphics, label, FigureLabelFont, new Rectangle(left, top, width, labelHeight), SubtleTextColor, Flags);
        TextRenderer.DrawText(graphics, value, FigureValueFont, new Rectangle(left, top + labelHeight, width, valueHeight), Color.White, Flags);
    }

    // Reads off the UI thread (the counters, the driver and WMI can take a
    // moment), one read at a time, then shows the figures if still there.
    private void StartRead()
    {
        if (!_read.IsCompleted)
            return;

        _read = Task.Run(() =>
            {
                if (!_memoryMhzRead)
                {
                    _memoryMhz = SystemInfoReader.ReadMemoryModules().Mhz;
                    _memoryMhzRead = true;
                }

                return (Cpu: _cpuReader.Read(), Gpu: _gpuReader.Read(), Memory: SystemInfoReader.ReadMemory());
            })
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && !IsDisposed)
                    ShowReadings(task.Result.Cpu, task.Result.Gpu, task.Result.Memory);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ShowReadings(CpuStats cpu, GpuStats gpu, MemoryUsage? memory)
    {
        var culture = CultureInfo.CurrentCulture;

        Show(_gauges[0], cpu.UsagePercent, second: HardwareStatsText.Gigahertz(cpu.CurrentMhz, culture));

        // A GPU that is powered down is idle, with nothing to read; saying so beats dashes.
        if (gpu.Asleep)
            Show(_gauges[1], 0, HardwareStatsText.NoReading, L.T("Asleep"));
        else
            Show(_gauges[1], gpu.UsagePercent, HardwareStatsText.Celsius(gpu.Celsius), HardwareStatsText.Megahertz(gpu.CoreMhz));

        if (memory is { TotalBytes: > 0 } ram)
        {
            var used = 100.0 * (ram.TotalBytes - ram.AvailableBytes) / ram.TotalBytes;
            Show(_gauges[2], used, HardwareStatsText.Megahertz(_memoryMhz), SystemInfoText.Size(ram.AvailableBytes, culture));
        }
    }

    // Repaints only when something shown changed.
    private void Show(Gauge gauge, double? percent = null, string? first = null, string? second = null)
    {
        var changed = false;

        if (percent is { } value && gauge.Percent != Math.Round(value))
        {
            // The ring sets off from wherever it is now (from empty the first time).
            gauge.From = gauge.Shown ?? 0;
            gauge.Shown ??= 0;
            gauge.Percent = Math.Round(value);
            gauge.StartedAt = _clock.Elapsed.TotalMilliseconds;
            _glideTimer.Start();
        }

        if (first is not null && first != gauge.First)
        {
            gauge.First = first;
            changed = true;
        }

        if (second is not null && second != gauge.Second)
        {
            gauge.Second = second;
            changed = true;
        }

        if (changed)
            Invalidate();
    }

    // One step of every moving ring: eased out, quick at first and settling
    // softly on the reading. Stops once all have arrived.
    private void Glide()
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        var moving = false;

        foreach (var gauge in _gauges)
        {
            if (gauge.Percent is not { } target || gauge.Shown == target)
                continue;

            var progress = Math.Clamp((now - gauge.StartedAt) / GlideMilliseconds, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            gauge.Shown = progress >= 1 ? target : gauge.From + (target - gauge.From) * eased;
            moving |= progress < 1;
        }

        Invalidate();

        if (!moving)
            _glideTimer.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _glideTimer.Stop();
            _glideTimer.Dispose();

            // A read may still be running on another thread; let it finish first.
            _read.ContinueWith(_ =>
            {
                _cpuReader.Dispose();
                _gpuReader.Dispose();
            }, TaskScheduler.Default);
        }

        base.Dispose(disposing);
    }

    private sealed class Gauge(string name, string firstLabel, string secondLabel)
    {
        public string Name { get; } = name;

        public string FirstLabel { get; } = firstLabel;

        public string SecondLabel { get; } = secondLabel;

        /// <summary>The latest reading, which the ring glides to; null until the first.</summary>
        public double? Percent { get; set; }

        /// <summary>What the ring shows now, on its way to <see cref="Percent"/>.</summary>
        public double? Shown { get; set; }

        /// <summary>Where the current glide set off from, and when (on the control's clock, in ms).</summary>
        public double From { get; set; }

        public double StartedAt { get; set; }

        public string First { get; set; } = HardwareStatsText.NoReading;

        public string Second { get; set; } = HardwareStatsText.NoReading;
    }
}
