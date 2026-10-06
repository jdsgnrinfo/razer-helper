using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The strip across the top of the main window, as Windows draws its own:
/// the app's logo and name at the left and, at the right, only the close
/// button, grey at rest and red under the pointer with its X in white.
/// It only reports the click; the window decides what it does.
/// </summary>
internal sealed class TitleBar : Control
{
    /// <summary>The strip's height.</summary>
    public static int BarHeight => S(34);

    private static readonly Color BarColor = Color.FromArgb(0x0A, 0x0A, 0x0A);
    private static readonly Color LineColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
    private static readonly Color TitleColor = Color.FromArgb(0xBB, 0xBB, 0xBB);
    private static readonly Color CloseHoverColor = Color.FromArgb(0xE8, 0x11, 0x23);
    private static readonly Font TitleFont = DesignFont(13);

    private readonly Image? _logo;
    private readonly CloseButton _close = new();

    public TitleBar(Image? logo)
    {
        _logo = logo;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = BarColor;
        Dock = DockStyle.Top;
        Height = BarHeight;
        Text = "RazerHelper";

        _close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_close);
        Resize += (_, _) => _close.Bounds = new Rectangle(Width - S(44), 0, S(44), Height - S(1));
    }

    /// <summary>Raised when the close button is clicked.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The close button, for its tooltip and screen-reader name.</summary>
    public Control CloseControl => _close;

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);

        using (var line = new SolidBrush(LineColor))
            graphics.FillRectangle(line, 0, Height - S(1), Width, S(1));

        var left = S(12);

        if (_logo is not null)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(_logo, left, (Height - S(1) - _logo.Height) / 2, _logo.Width, _logo.Height);
            left += _logo.Width + S(8);
        }

        TextRenderer.DrawText(graphics, Text, TitleFont, new Rectangle(left, 0, _close.Left - left, Height - S(1)), TitleColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    // The close button: an X in thin lines; red behind it under the pointer.
    private sealed class CloseButton : Control
    {
        private bool _hovered;

        public CloseButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.PushButton;
            BackColor = BarColor;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(_hovered ? CloseHoverColor : BackColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var half = S(5f);
            var middle = new PointF(Width / 2f, Height / 2f);
            using var pen = new Pen(_hovered ? Color.White : Color.FromArgb(0xAA, 0xAA, 0xAA), S(1.2f));
            graphics.DrawLine(pen, middle.X - half, middle.Y - half, middle.X + half, middle.Y + half);
            graphics.DrawLine(pen, middle.X + half, middle.Y - half, middle.X - half, middle.Y + half);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
    }
}
