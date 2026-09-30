using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The small notice at the top right of the screen after a performance
/// shortcut: the mode's icon, its name, and how it went ("Active", "Needs to
/// be plugged in"). It never takes the focus, so a game in front keeps its
/// keyboard, stays above other windows, and fades out by itself. Pressing
/// another shortcut while it shows just updates it.
/// </summary>
internal sealed class ProfileToast : Form
{
    private const int ShownMilliseconds = 2_000;
    // Per 15 ms tick: the fade-out takes Motion.Milliseconds (300 ms).
    private const double FadeStep = 15 / Motion.Milliseconds;

    // How far the fade-out is, 1 fully shown to 0 gone, moving evenly; the
    // opacity follows it eased.
    private double _fadeLevel = 1;

    private const int WsExTopmost = 0x00000008;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private static int IconBox => S(40);
    private static int ScreenMargin => S(16);

    private static readonly Font NameFont = DesignFont(13, FontStyle.Bold);
    private static readonly Font StatusFont = DesignFont(11);

    private readonly System.Windows.Forms.Timer _hideTimer = new() { Interval = ShownMilliseconds };
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 15 };

    private Glyph _icon;
    private string _name = string.Empty;
    private string _status = string.Empty;
    private bool _applied;

    public ProfileToast()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = ButtonColor;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = S(new Size(260, 64));
        TopMost = true;

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            _fadeTimer.Start();
        };

        _fadeTimer.Tick += (_, _) =>
        {
            _fadeLevel = Math.Max(0, _fadeLevel - FadeStep);
            Opacity = Motion.Ease((float)_fadeLevel);

            if (_fadeLevel > 0)
                return;

            _fadeTimer.Stop();
            Hide();
        };
    }

    /// <summary>Shows (or updates) the notice at the top right of the screen the pointer is on.</summary>
    /// <param name="applied">Whether the mode is now on: its icon is green, and grey when it was refused.</param>
    public void ShowNotice(Glyph icon, string name, string status, bool applied)
    {
        (_icon, _name, _status, _applied) = (icon, name, status, applied);

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(workingArea.Right - Width - ScreenMargin, workingArea.Top + ScreenMargin);

        _fadeTimer.Stop();
        _fadeLevel = 1;
        Opacity = 1;
        Invalidate();

        if (!Visible)
            Show();

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    // Never steal the focus from the game or program in front.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);

        // The icon in a circle like the mode buttons': dark on green when the
        // mode is on, green on grey when it was refused.
        var padding = (Height - IconBox) / 2;
        var box = new Rectangle(padding, padding, IconBox, IconBox);

        using (var fill = new SolidBrush(_applied ? RazerGreen : IconCircleColor))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillEllipse(fill, box);
        }

        var glyph = S(22);
        Glyphs.Draw(graphics, _icon, new RectangleF(box.X + (IconBox - glyph) / 2f, box.Y + (IconBox - glyph) / 2f, glyph, glyph),
            _applied ? OnGreenTextColor : SubtleTextColor);

        // The name above the status, both beside the icon.
        var textLeft = box.Right + S(12);
        var textWidth = Width - textLeft - padding;
        const TextFormatFlags Line = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        TextRenderer.DrawText(graphics, _name, NameFont, new Rectangle(textLeft, box.Y + S(2), textWidth, S(18)), Color.White, Line);
        TextRenderer.DrawText(graphics, _status, StatusFont, new Rectangle(textLeft, box.Y + S(22), textWidth, S(16)), SubtleTextColor, Line);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
