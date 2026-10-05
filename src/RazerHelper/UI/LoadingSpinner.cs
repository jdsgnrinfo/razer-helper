using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A green arc turning around a faint ring, for while something is being read.
/// Turns only while shown, so a hidden one costs nothing.
/// </summary>
internal sealed class LoadingSpinner : Control
{
    // Base-design pixels.
    private const int DefaultDiameter = 36;
    private const float Thickness = 4f;

    // One full turn a second, as a quarter-circle arc.
    private const float DegreesPerSecond = 360f;
    private const float ArcDegrees = 90f;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _clock = new();

    /// <summary>The circle's size in base-design pixels: 36 unless set smaller, as in a button's place.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Diameter { get; init; } = DefaultDiameter;

    public LoadingSpinner()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        _timer.Tick += (_, _) => Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            _clock.Start();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _clock.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var size = S(Diameter);
        var inset = S(Thickness) / 2;
        var circle = new RectangleF((Width - size) / 2f + inset, (Height - size) / 2f + inset, size - 2 * inset, size - 2 * inset);
        var start = (float)(_clock.Elapsed.TotalSeconds * DegreesPerSecond % 360);

        using (var track = new Pen(DividerColor, S(Thickness)))
            graphics.DrawEllipse(track, circle);

        using var arc = new Pen(RazerGreen, S(Thickness)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawArc(arc, circle, start, ArcDegrees);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();

        base.Dispose(disposing);
    }
}
