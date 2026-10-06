using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The value bubble over a slider's thumb while it is dragged or moved with
/// the keys: a green pill with the value in dark text and a small point down
/// to the thumb. It springs up from the thumb as it appears and its digits
/// roll as the value changes.
/// </summary>
internal static class BubbleArt
{
    public static int PillHeight => S(26);
    public static int PointerSize => S(5);
    private static int SidePadding => S(10);

    public static readonly Font TextFont = SemiBoldFont(14);

    private static readonly StringFormat Typographic = CreateFormat();

    /// <summary>The pill's width for the text it shows now.</summary>
    public static float Width(Graphics graphics, RollingText text) =>
        text.Text.Sum(c => Measure(graphics, c.ToString()).Width) + 2 * SidePadding;

    /// <summary>
    /// Paints the bubble with its point's tip at <paramref name="tip"/>, its
    /// pill moved sideways by <paramref name="shift"/> (to stay inside the
    /// slider), at <paramref name="appear"/> from 0 (gone) to 1 (fully up).
    /// </summary>
    public static void Paint(Graphics graphics, PointF tip, float shift, RollingText text, float appear)
    {
        var opacity = Math.Clamp(appear * 2.2f, 0, 1);

        if (opacity <= 0)
            return;

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // It grows from its point, rising a little as it comes.
        var scale = 0.6f + 0.4f * appear;
        graphics.TranslateTransform(tip.X, tip.Y + (1 - Math.Min(appear, 1)) * S(6));
        graphics.ScaleTransform(scale, scale);

        var width = Width(graphics, text);
        var pill = new RectangleF(-width / 2 + shift, -PointerSize - PillHeight, width, PillHeight);
        var green = Color.FromArgb((int)(opacity * 255), RazerGreen);

        using (var fill = new SolidBrush(green))
        {
            using (var shape = RoundedButton.RoundedPath(pill, PillHeight / 2f))
                graphics.FillPath(fill, shape);

            graphics.FillPolygon(fill, [new PointF(-PointerSize, -PointerSize - 1), new PointF(PointerSize, -PointerSize - 1), new PointF(0, 0)]);
        }

        using var ink = new SolidBrush(OnGreenTextColor);
        text.Paint(
            graphics,
            RectangleF.Inflate(pill, -SidePadding + 1, 0),
            HorizontalAlignment.Center,
            part => Measure(graphics, part),
            (part, at, alpha) =>
            {
                ink.Color = Color.FromArgb((int)(Math.Clamp(alpha * opacity, 0, 1) * 255), OnGreenTextColor);
                graphics.DrawString(part, TextFont, ink, at, Typographic);
            });

        graphics.Restore(state);
    }

    private static SizeF Measure(Graphics graphics, string text) => graphics.MeasureString(text, TextFont, PointF.Empty, Typographic);

    private static StringFormat CreateFormat()
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return format;
    }
}

/// <summary>
/// The bubble for the ordinary sliders, which are too short to hold it: a
/// small see-through window over the thumb that never takes the focus or the
/// mouse. One serves every slider, since only one moves at a time.
/// </summary>
internal sealed class SliderBubble : Form
{
    private static SliderBubble? _shared;

    private readonly RollingText _text = new();
    private readonly Spring _appear = new(0, 0.002);
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();

    private Control? _owner;
    private Point _tip;
    private int _left;
    private int _right;
    private bool _showing;

    private SliderBubble()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        _frames.Tick += (_, _) => Frame();
    }

    /// <summary>The one bubble, made on first use (on the UI thread).</summary>
    public static SliderBubble Shared => _shared ??= new SliderBubble();

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int Layered = 0x80000, ClickThrough = 0x20, ToolWindow = 0x80, NoActivate = 0x8000000, Topmost = 0x8;
            var parameters = base.CreateParams;
            parameters.ExStyle |= Layered | ClickThrough | ToolWindow | NoActivate | Topmost;
            return parameters;
        }
    }

    /// <summary>
    /// Shows the bubble for <paramref name="owner"/> with its point at
    /// <paramref name="tip"/> (screen), kept between <paramref name="left"/>
    /// and <paramref name="right"/>, showing <paramref name="text"/>.
    /// </summary>
    public void Follow(Control owner, Point tip, int left, int right, string text)
    {
        var reduced = Motion.Reduced;
        _text.Set(text, animate: _showing && _owner == owner && !reduced);
        _owner = owner;
        _tip = tip;
        _left = left;
        _right = right;

        if (!_showing)
        {
            _showing = true;

            if (reduced)
                _appear.Jump(1);
            else
                _appear.To(1, Spring.Snappy);
        }

        Render();

        if (!Visible)
            Show();

        StartFrames();
    }

    /// <summary>Lets the bubble go, if <paramref name="owner"/> is the slider it is showing for.</summary>
    public void Release(Control owner)
    {
        if (owner != _owner || !_showing)
            return;

        _showing = false;

        if (Motion.Reduced)
            _appear.Jump(0);
        else
            _appear.To(0, Spring.Snappy);

        StartFrames();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    private void StartFrames()
    {
        // Called on every move of the thumb, so only a stopped clock starts over.
        if (_frames.Enabled)
            return;

        _clock.Restart();
        _frames.Start();
    }

    private void Frame()
    {
        var seconds = Math.Min(0.032, _clock.Elapsed.TotalSeconds);
        _clock.Restart();
        _appear.Step(seconds);

        if (!_showing && _appear.Position <= 0.02)
        {
            _appear.Jump(0);
            _frames.Stop();
            Hide();
            return;
        }

        Render();

        if (!_appear.Moving && !_text.Rolling)
            _frames.Stop();
    }

    // Draws the bubble into a bitmap with see-through edges and hands it to Windows.
    private void Render()
    {
        using var measuring = Graphics.FromHwnd(IntPtr.Zero);
        var width = (int)Math.Ceiling(BubbleArt.Width(measuring, _text));
        var margin = S(6);
        var height = BubbleArt.PillHeight + BubbleArt.PointerSize + S(6) + margin;

        // The pill centres on the thumb unless that would take it past the slider's ends.
        var pillLeft = Math.Clamp(_tip.X - width / 2, _left, Math.Max(_left, _right - width));
        var shift = pillLeft - (_tip.X - width / 2);

        var windowLeft = Math.Min(pillLeft, _tip.X - BubbleArt.PointerSize) - margin;
        var windowWidth = Math.Max(pillLeft + width, _tip.X + BubbleArt.PointerSize) + margin - windowLeft;
        var windowTop = _tip.Y - height + S(6);

        using var bitmap = new Bitmap(windowWidth, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            BubbleArt.Paint(graphics, new PointF(_tip.X - windowLeft, height - S(6)), shift, _text, (float)_appear.Position);
        }

        Apply(bitmap, new Point(windowLeft, windowTop));
    }

    private void Apply(Bitmap bitmap, Point location)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var handle = bitmap.GetHbitmap(Color.FromArgb(0));
        var previous = SelectObject(memory, handle);

        try
        {
            var size = new NativeSize { Width = bitmap.Width, Height = bitmap.Height };
            var source = new NativePoint();
            var destination = new NativePoint { X = location.X, Y = location.Y };
            var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref source, 0, ref blend, 2);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(handle);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destination, ref NativePoint destinationPoint, ref NativeSize size, IntPtr source, ref NativePoint sourcePoint, int key, ref BlendFunction blend, int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr context);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr context, IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
