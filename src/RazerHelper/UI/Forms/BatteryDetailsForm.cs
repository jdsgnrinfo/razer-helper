using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Battery details window, opened from the Battery section: what the
/// battery is doing now (power in or out, time left or to full, charge) and
/// how worn it is (capacity now against when new). Styled like Settings:
/// a title, the close X, Esc closes. It reads the battery every couple of
/// seconds, and only while it is open.
/// </summary>
internal sealed class BatteryDetailsForm : Form
{
    private const int RefreshIntervalMilliseconds = 2_000;

    private static int ContentWidth => S(340);
    private static int LabelWidth => S(80);

    private readonly Func<BatteryDetails?> _read;
    private readonly TableLayoutPanel _rows;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };
    private Form? _anchor;
    private bool _loggedFailure;

    /// <param name="read">Reads the battery; the app passes <see cref="BatteryReader.Read"/>.</param>
    public BatteryDetailsForm(Func<BatteryDetails?> read)
    {
        _read = read;

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
        Text = "RazerHelper Battery";

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(16, 14, 16, 14)),
            WrapContents = false
        };

        layout.Controls.Add(CreateTitleRow());

        _rows = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Width = ContentWidth
        };

        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth - LabelWidth));
        layout.Controls.Add(_rows);

        Controls.Add(layout);

        ShowDetails();
        _refreshTimer.Tick += (_, _) => ShowDetails();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    // Rebuilds the rows from a fresh reading. A failed read shows one line
    // saying so rather than stale figures, and is logged once.
    private void ShowDetails()
    {
        IReadOnlyList<(string Label, string Value)> rows;

        try
        {
            rows = _read() is { } battery
                ? BatteryDetailsText.Rows(battery)
                : [("Battery", "No battery information available")];
        }
        catch (Exception exception)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error("Could not read the battery details.", exception);
            }

            rows = [("Battery", "Could not read the battery")];
        }

        _rows.SuspendLayout();

        // Same rows as last time (the usual case): only the values change.
        if (_rows.RowCount == rows.Count && _rows.Controls.Count == rows.Count * 2 &&
            rows.Select((row, index) => _rows.GetControlFromPosition(0, index)?.Text == row.Label).All(same => same))
        {
            for (var index = 0; index < rows.Count; index++)
                _rows.GetControlFromPosition(1, index)!.Text = rows[index].Value;
        }
        else
        {
            _rows.Controls.Clear();
            _rows.RowStyles.Clear();
            _rows.RowCount = rows.Count;

            for (var index = 0; index < rows.Count; index++)
            {
                _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _rows.Controls.Add(CreateCell(rows[index].Label, SubtleTextColor), 0, index);
                _rows.Controls.Add(CreateCell(rows[index].Value, Color.White), 1, index);
            }
        }

        _rows.ResumeLayout();
    }

    private static Label CreateCell(string text, Color color) => new()
    {
        AutoSize = true,
        Font = GetDesignFont("Segoe UI", 9.5F),
        ForeColor = color,
        Margin = S(new Padding(0, 3, 0, 3)),
        MaximumSize = new Size(ContentWidth - LabelWidth, 0),
        Text = text
    };

    // "Battery" on the left, the close X on the right, as in Settings.
    private Control CreateTitleRow()
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Height = S(30),
            Margin = S(new Padding(0, 0, 0, 10)),
            Padding = Padding.Empty,
            RowCount = 1,
            Width = ContentWidth
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        row.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = GetDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Margin = Padding.Empty,
            Text = "Battery"
        }, 0, 0);

        var close = new GlyphButton(Glyph.Close, S(18))
        {
            AccessibleName = "Close",
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = S(new Size(28, 28))
        };

        close.Click += (_, _) => Close();
        row.Controls.Add(close, 1, 0);
        return row;
    }
}
