using System.Globalization;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Custom mode's window beside the popup, like Settings: what the CPU and
/// the GPU are doing now, side by side, then their boost levels. Each click
/// applies at once, so there is only a close X (Esc works too). The selectors
/// belong to the Performance section, which keeps them up to date; this
/// window borrows them and hands them back. The figures are read every couple
/// of seconds, only while the window is open.
/// </summary>
internal sealed class CustomBoostForm : Form
{
    private const int RefreshIntervalMilliseconds = 2_000;

    private static int ContentWidth => S(400);
    private static int CardGap => S(8);
    private static Padding CardPadding => S(new Padding(10, 8, 10, 8));
    private static int StatTitleHeight => S(26);
    private static int StatLineHeight => S(22);

    // The GPU card has the most lines; both cards are that tall, so they line up.
    private const int MostStatLines = 4;

    private readonly CustomBoostRow _selectors;
    private readonly CpuStatsReader _cpuReader = new();
    private readonly GpuStatsReader _gpuReader = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };

    private readonly Label _cpuTemperature;
    private readonly Label _cpuUsage;
    private readonly Label _cpuSpeed;
    private readonly Label _gpuState;
    private readonly Label _gpuTemperature;
    private readonly Label _gpuUsage;
    private readonly Label _gpuCoreClock;
    private readonly Label _gpuMemoryClock;

    private Form? _anchor;
    private Task _read = Task.CompletedTask;

    public CustomBoostForm(CustomBoostRow selectors)
    {
        _selectors = selectors;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont("Segoe UI", 9F);
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
            Padding = S(new Padding(16, 14, 16, 16)),
            WrapContents = false
        };

        layout.Controls.Add(WindowTitleRow.Create(this, "Custom", ContentWidth));

        // What the two chips are doing now, side by side.
        var (cpuCard, _, cpuValues) = CreateStatsCard("CPU", Glyph.Cpu,
            "Temperature", "Usage", "Speed");
        (_cpuTemperature, _cpuUsage, _cpuSpeed) = (cpuValues[0], cpuValues[1], cpuValues[2]);

        var (gpuCard, gpuNote, gpuValues) = CreateStatsCard("GPU", Glyph.Gpu,
            "Temperature", "Usage", "Core clock", "Memory clock");
        _gpuState = gpuNote;
        (_gpuTemperature, _gpuUsage, _gpuCoreClock, _gpuMemoryClock) =
            (gpuValues[0], gpuValues[1], gpuValues[2], gpuValues[3]);

        var stats = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, CardGap),
            Padding = Padding.Empty,
            RowCount = 1,
            Size = new Size(ContentWidth, StatsCardHeight)
        };

        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cpuCard.Margin = new Padding(0, 0, CardGap / 2, 0);
        gpuCard.Margin = new Padding(CardGap / 2, 0, 0, 0);
        stats.Controls.Add(cpuCard, 0, 0);
        stats.Controls.Add(gpuCard, 1, 0);
        layout.Controls.Add(stats);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 8F),
            ForeColor = SubtleTextColor,
            Margin = S(new Padding(0, 2, 0, 8)),
            MaximumSize = new Size(ContentWidth, 0),
            Text = L.T("Boost levels apply right away and are saved in the current power profile.")
        });

        var selectorsCard = new CardPanel
        {
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = CardPadding,
            RowCount = 1,
            Size = new Size(ContentWidth, CustomBoostRow.RowHeight + CardPadding.Vertical)
        };

        _selectors.Dock = DockStyle.Fill;
        selectorsCard.Controls.Add(_selectors, 0, 0);
        layout.Controls.Add(selectorsCard);

        Controls.Add(layout);

        _refreshTimer.Tick += (_, _) => StartRead();
    }

    private static int StatsCardHeight => CardPadding.Vertical + StatTitleHeight + MostStatLines * StatLineHeight;

    /// <summary>
    /// The CPU temperature, from the laptop's controller. It is already read
    /// for the popup's header, so the host passes each reading on rather than
    /// this window asking the controller again.
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
        WindowChrome.Apply(Handle, BorderColor);
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
        _selectors.Parent?.Controls.Remove(_selectors);
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

        _gpuState.Text = gpu.Asleep ? L.T("Asleep") : string.Empty;
        _gpuTemperature.Text = HardwareStatsText.Celsius(gpu.Celsius);
        _gpuUsage.Text = HardwareStatsText.Percent(gpu.UsagePercent);
        _gpuCoreClock.Text = HardwareStatsText.Megahertz(gpu.CoreMhz);
        _gpuMemoryClock.Text = HardwareStatsText.Megahertz(gpu.MemoryMhz);
    }

    // A card with the chip's name (and a note on the right, such as "Asleep"),
    // then one line per figure: its name on the left, the value on the right.
    // Returns the value labels in the order the names were given.
    private static (CardPanel Card, Label Note, Label[] Values) CreateStatsCard(string title, Glyph glyph, params string[] names)
    {
        var card = new CardPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = CardPadding,
            RowCount = names.Length + 2
        };

        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, StatTitleHeight));

        // The title with its icon, bold like the selectors' titles below.
        var heading = CreateSectionLabel(title, glyph);
        heading.Dock = DockStyle.Fill;
        heading.Font = GetDesignFont("Segoe UI", 9.5F, FontStyle.Bold);
        heading.Margin = Padding.Empty;
        heading.BackColor = CardColor;
        card.Controls.Add(heading, 0, 0);

        var note = CreateStatLabel(SubtleTextColor, ContentAlignment.MiddleRight);
        note.Font = GetDesignFont("Segoe UI", 8.5F);
        card.Controls.Add(note, 1, 0);

        var values = new Label[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, StatLineHeight));

            var name = CreateStatLabel(Color.Silver, ContentAlignment.MiddleLeft);
            name.Text = L.T(names[index]);
            card.Controls.Add(name, 0, index + 1);

            values[index] = CreateStatLabel(Color.White, ContentAlignment.MiddleRight);
            values[index].Text = HardwareStatsText.NoReading;
            card.Controls.Add(values[index], 1, index + 1);
        }

        // Whatever height is left over, so a shorter card still fills its column.
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return (card, note, values);
    }

    private static Label CreateStatLabel(Color color, ContentAlignment alignment) => new()
    {
        AutoSize = true,
        BackColor = CardColor,
        Dock = DockStyle.Fill,
        Font = GetDesignFont("Segoe UI", 9F),
        ForeColor = color,
        Margin = Padding.Empty,
        TextAlign = alignment
    };
}
