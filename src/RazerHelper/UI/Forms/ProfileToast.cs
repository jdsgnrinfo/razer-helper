using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The small notice at the top right of the screen after a performance
/// shortcut, drawn like a game HUD: a strip in Chroma's colors down the left
/// edge, the top right corner cut on a slant, the mode's icon in green, then
/// "PERFORMANCE MODE" small and spaced, the mode's name large in capitals, and
/// how it went ("Active", "Needs to be plugged in"). A refused mode turns the
/// strip amber and the icon grey. It never takes the focus, so a game in front keeps its
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

    private static int EdgeWidth => S(4);
    private static int CornerCut => S(14);

    // Chroma's colors, for the edge; amber in their place when the mode was refused.
    private static readonly Color[] ChromaColors =
    [
        Color.FromArgb(0xFF, 0x2D, 0x55), Color.FromArgb(0xFF, 0x9F, 0x0A), Color.FromArgb(0xFF, 0xD6, 0x0A), RazerGreen,
        Color.FromArgb(0x32, 0xD2, 0xFF), Color.FromArgb(0x5E, 0x5C, 0xE6), Color.FromArgb(0xBF, 0x5A, 0xF2)
    ];

    private static readonly Color RefusedColor = Color.FromArgb(0xFF, 0x9F, 0x0A);
    private static int ScreenMargin => S(16);

    private static readonly Font LabelFont = SemiBoldFont(11);
    private static readonly Font NameFont = DesignFont(21, FontStyle.Bold);
    private static readonly Font StatusFont = DesignFont(12);

    private readonly System.Windows.Forms.Timer _hideTimer = new() { Interval = ShownMilliseconds };
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 15 };

    private Glyph _icon;
    private string _name = string.Empty;
    private string _status = string.Empty;
    private bool _applied;

    public ProfileToast()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(0x0E, 0x0E, 0x0E);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = S(new Size(300, 72));
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

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        // The top right corner cut on a slant. A window with its own shape is
        // left square by Windows 11, so no rounding gets in the way.
        var cut = CornerCut;
        using var shape = new System.Drawing.Drawing2D.GraphicsPath();
        shape.AddPolygon([new Point(0, 0), new Point(Width - cut, 0), new Point(Width, cut), new Point(Width, Height), new Point(0, Height)]);
        Region = new Region(shape);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var accent = _applied ? RazerGreen : RefusedColor;

        // The edge at the left: Chroma's colors top to bottom, or amber when the mode was refused.
        var edge = new Rectangle(0, 0, EdgeWidth, Height);

        if (_applied)
        {
            using var chroma = new System.Drawing.Drawing2D.LinearGradientBrush(edge, Color.Red, Color.Blue, System.Drawing.Drawing2D.LinearGradientMode.Vertical)
            {
                InterpolationColors = new System.Drawing.Drawing2D.ColorBlend
                {
                    Colors = ChromaColors,
                    Positions = [.. Enumerable.Range(0, ChromaColors.Length).Select(index => index / (float)(ChromaColors.Length - 1))]
                }
            };
            graphics.FillRectangle(chroma, edge);
        }
        else
        {
            using var amber = new SolidBrush(RefusedColor);
            graphics.FillRectangle(amber, edge);
        }

        // The mode's icon, green, or grey when it was refused.
        var iconSize = S(30);
        var iconLeft = EdgeWidth + S(18);
        Glyphs.Draw(graphics, _icon, new RectangleF(iconLeft, (Height - iconSize) / 2f, iconSize, iconSize), _applied ? RazerGreen : SubtleTextColor);

        // "PERFORMANCE MODE" small and spaced in the accent, the mode's name
        // large and spaced in white, and how it went in grey.
        var left = iconLeft + iconSize + S(14);
        var top = (Height - S(52)) / 2;
        var culture = System.Globalization.CultureInfo.CurrentCulture;

        DrawSpaced(graphics, Core.Localization.L.T("Performance mode").ToUpper(culture), LabelFont, accent, left, top, S(2.5f));
        DrawSpaced(graphics, _name.ToUpper(culture), NameFont, Color.White, left, top + S(13), S(2f));
        TextRenderer.DrawText(graphics, _status, StatusFont, new Point(left, top + S(37)), SubtleTextColor, TextFormatFlags.NoPadding);
    }

    // Text with room between its letters, which GDI cannot do on its own.
    private static void DrawSpaced(Graphics graphics, string text, Font font, Color color, float left, float top, float spacing)
    {
        const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        var x = left;

        foreach (var letter in text)
        {
            var piece = letter.ToString();
            TextRenderer.DrawText(graphics, piece, font, new Point((int)Math.Round(x), (int)Math.Round(top)), color, Flags);
            x += TextRenderer.MeasureText(graphics, piece, font, Size.Empty, Flags).Width + spacing;
        }
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
