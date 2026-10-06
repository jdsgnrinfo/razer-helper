using System.Diagnostics;
using System.Drawing.Drawing2D;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>What the keyboard preview plays.</summary>
internal enum PreviewEffect
{
    Static,
    Breathing,
    Spectrum,
    Wave,
    Off
}

/// <summary>
/// Beside the keyboard's effects: a panel, a step lighter than the page with
/// the buttons' corners and no outline, holding the effect's name with
/// Reset at the right; the colors to choose from, as circles, the chosen one
/// ringed in white and a little larger (only where the laptop shows a chosen
/// color); and a small keyboard playing the effect in that color at the
/// brightness set. It only reports clicks; its owner applies them.
/// </summary>
internal sealed class LightingPreview : Control
{
    private static readonly Color PanelColor = Color.FromArgb(0x18, 0x18, 0x18);
    private static readonly Color KeyboardColor = Color.FromArgb(0x11, 0x11, 0x11);
    private static readonly Font NameFont = SemiBoldFont(15);
    private static readonly Font LabelFont = DesignFont(13);

    private static int Inset => S(14);
    private static int SwatchSize => S(26);
    private static int SwatchGap => S(8);
    private static int KeyboardHeight => S(64);

    private const int KeyColumns = 14;
    private const int KeyRows = 4;

    private readonly Color[] _colors;
    private readonly Button _reset;
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 33 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _chosenColor = -1;
    private PreviewEffect _effect = PreviewEffect.Static;
    private Color _color = RazerGreen;
    private int _brightness = 100;
    private string _effectName = string.Empty;

    /// <param name="colors">The colors offered, or empty where the laptop shows no chosen color.</param>
    public LightingPreview(IReadOnlyList<Color> colors)
    {
        _colors = [.. colors];

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        // The panel's own color, which Reset takes for its corners; the page's shows past the panel's.
        BackColor = PanelColor;

        _reset = UiControls.CreateSmallButton("Reset");
        _reset.Click += (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_reset);

        _frames.Tick += (_, _) => Invalidate();
        Resize += (_, _) => _reset.Location = new Point(Width - Inset - _reset.Width, Inset);
    }

    /// <summary>Raised with the index of the color clicked.</summary>
    public event EventHandler<int>? ColorPicked;

    /// <summary>Raised when Reset is clicked.</summary>
    public event EventHandler? ResetRequested;

    /// <summary>The panel's height for its width: the name, the colors when offered, and the keyboard.</summary>
    public int PreferredHeight => KeyboardTop + KeyboardHeight + Inset;

    /// <summary>Shows what the keyboard does: its effect and that effect's name, the color lit (-1 when none of the offered ones) and the brightness.</summary>
    public void Show(PreviewEffect effect, string effectName, int colorIndex, Color color, int brightness)
    {
        _effect = effect;
        _effectName = effectName;
        _chosenColor = colorIndex;
        _color = color;
        _brightness = brightness;
        UpdateFrames();
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateFrames();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = SwatchAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button == MouseButtons.Left && SwatchAt(e.Location) is var index and >= 0)
            ColorPicked?.Invoke(this, index);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var panel = RoundedButton.RoundedPath(new RectangleF(0, 0, Width, Height), S(RoundedButton.CornerRadius)))
        using (var fill = new SolidBrush(PanelColor))
            graphics.FillPath(fill, panel);

        TextRenderer.DrawText(graphics, _effectName, NameFont, new Rectangle(Inset, Inset, _reset.Left - Inset - S(8), _reset.Height), Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (_colors.Length > 0)
        {
            TextRenderer.DrawText(graphics, L.T("Color"), LabelFont, new Point(Inset, ColorLabelTop), SubtleTextColor, TextFormatFlags.NoPadding);

            for (var index = 0; index < _colors.Length; index++)
                PaintSwatch(graphics, SwatchBounds(index), _colors[index], index == _chosenColor);
        }

        PaintKeyboard(graphics, new RectangleF(Inset, KeyboardTop, Width - 2 * Inset, KeyboardHeight));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    private int ColorLabelTop => Inset + _reset.Height + S(12);

    private int SwatchesTop => ColorLabelTop + S(20);

    private int SwatchesPerRow => Math.Max(1, (Width - 2 * Inset + SwatchGap) / (SwatchSize + SwatchGap));

    private int KeyboardTop => _colors.Length == 0
        ? Inset + _reset.Height + S(12)
        : SwatchesTop + ((_colors.Length + SwatchesPerRow - 1) / SwatchesPerRow) * (SwatchSize + SwatchGap) + S(4);

    private Rectangle SwatchBounds(int index) => new(
        Inset + index % SwatchesPerRow * (SwatchSize + SwatchGap),
        SwatchesTop + index / SwatchesPerRow * (SwatchSize + SwatchGap),
        SwatchSize,
        SwatchSize);

    private int SwatchAt(Point point)
    {
        for (var index = 0; index < _colors.Length; index++)
        {
            if (SwatchBounds(index).Contains(point))
                return index;
        }

        return -1;
    }

    // A color as a circle; the chosen one a little larger, ringed in white with a dark gap.
    private void PaintSwatch(Graphics graphics, Rectangle bounds, Color color, bool chosen)
    {
        var box = chosen ? RectangleF.Inflate(bounds, S(1.5f), S(1.5f)) : bounds;

        using (var fill = new SolidBrush(color))
            graphics.FillEllipse(fill, box);

        if (chosen)
        {
            using var gap = new Pen(PanelColor, S(2f));
            graphics.DrawEllipse(gap, RectangleF.Inflate(box, -S(2.5f), -S(2.5f)));

            using var ring = new Pen(Color.White, S(2f));
            graphics.DrawEllipse(ring, RectangleF.Inflate(box, -S(1f), -S(1f)));
        }
        else
        {
            using var edge = new Pen(Color.FromArgb(64, Color.Black), S(2f));
            graphics.DrawEllipse(edge, RectangleF.Inflate(box, -S(1f), -S(1f)));
        }
    }

    // The keys, in rows, each lit by the effect at this moment.
    private void PaintKeyboard(Graphics graphics, RectangleF bounds)
    {
        using (var board = RoundedButton.RoundedPath(bounds, S(RoundedButton.CornerRadius)))
        using (var fill = new SolidBrush(KeyboardColor))
            graphics.FillPath(fill, board);

        var gap = S(3f);
        var inner = RectangleF.Inflate(bounds, -S(6f), -S(6f));
        var keyWidth = (inner.Width - (KeyColumns - 1) * gap) / KeyColumns;
        var keyHeight = (inner.Height - (KeyRows - 1) * gap) / KeyRows;
        var time = Motion.Reduced ? 0 : _clock.Elapsed.TotalSeconds;
        var level = 0.15f + 0.85f * Math.Clamp(_brightness, 0, 100) / 100f;

        for (var row = 0; row < KeyRows; row++)
        {
            for (var column = 0; column < KeyColumns; column++)
            {
                var (color, strength) = KeyLight(column, time);
                using var key = new SolidBrush(Color.FromArgb((int)(255 * Math.Clamp(strength * level, 0, 1)), color));
                graphics.FillRectangle(key, inner.Left + column * (keyWidth + gap), inner.Top + row * (keyHeight + gap), keyWidth, keyHeight);
            }
        }
    }

    // One key's color and strength at <paramref name="time"/>, by its column.
    private (Color Color, float Strength) KeyLight(int column, double time) => _effect switch
    {
        PreviewEffect.Breathing => (_color, (float)(0.25 + 0.6 * (Math.Sin(time * 1000 / 600) + 1) / 2)),
        PreviewEffect.Spectrum => (Hue(time * 1000 / 20), 0.85f),
        PreviewEffect.Wave => (Hue(column * 25 - time * 1000 / 8), 0.85f),
        PreviewEffect.Off => (Color.White, 0.06f),
        _ => (_color, 0.85f)
    };

    // A bright color of the hue given in degrees.
    private static Color Hue(double degrees)
    {
        var hue = ((degrees % 360) + 360) % 360 / 60;
        var x = (float)(1 - Math.Abs(hue % 2 - 1));
        var (r, g, b) = (int)hue switch
        {
            0 => (1f, x, 0f),
            1 => (x, 1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x, 1f),
            4 => (x, 0f, 1f),
            _ => (1f, 0f, x)
        };

        // About the 85% saturation and 55% lightness of the design.
        static int Channel(float value) => (int)(40 + value * 200);
        return Color.FromArgb(Channel(r), Channel(g), Channel(b));
    }

    // Only the moving effects need frames, and only while on screen.
    private void UpdateFrames()
    {
        var moving = _effect is PreviewEffect.Breathing or PreviewEffect.Spectrum or PreviewEffect.Wave;

        if (moving && Visible && !Motion.Reduced)
            _frames.Start();
        else
            _frames.Stop();
    }
}
