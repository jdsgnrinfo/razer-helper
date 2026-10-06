using System.Drawing.Drawing2D;
using System.Globalization;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>The main window's sections, in the sidebar's order.</summary>
internal enum DashboardPage
{
    Performance,
    Display,
    Audio,
    Power,
    System,
    Optimize,
    Settings
}

/// <summary>
/// The left of the main window: a rounded card set in from the window's
/// edges, with no outline, holding the app's logo, one entry per section in
/// capitals (see SidebarNav: a green pill on the one on show and a highlight
/// under the pointer, both gliding between entries) and, at its foot, this
/// computer's name under a green laptop.
/// An error shows in red just above the computer, only until the next result.
/// It only reports clicks; the window decides what they do.
/// </summary>
internal sealed class Sidebar : Panel
{
    /// <summary>The card and the window's margin to its left.</summary>
    public static int SidebarWidth => CardMargin + S(240);

    // The gap between the card and the window's edges, above, below and to the left.
    private static int CardMargin => S(12);

    private static int CardRadius => S(10);

    private static int Inset => S(12);

    private readonly SidebarNav _nav;
    private readonly Label _error;
    private readonly DeviceBadge _device;
    private readonly ThemedToolTip _toolTip = new();

    public Sidebar(Image? logo)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = BackgroundColor;
        Dock = DockStyle.Left;
        Margin = Padding.Empty;

        // The parts fill the card's width, so the selected entry's glow can
        // spread beside it; they keep their inset as margins instead. Above and
        // below, at least the card's corner, so the parts never square it off.
        Padding = new Padding(CardMargin, CardMargin + S(14), 0, CardMargin + CardRadius);
        Width = SidebarWidth;

        var entryWidth = SidebarWidth - CardMargin - 2 * Inset;

        var top = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SidebarColor,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            WrapContents = false
        };

        top.Controls.Add(new BrandRow(logo)
        {
            Margin = new Padding(Inset, 0, Inset, 0),
            Size = new Size(entryWidth, S(40))
        });

        _nav = new SidebarNav(new[]
        {
            (DashboardPage.Performance, "Performance"),
            (DashboardPage.Display, "Display and lighting"),
            (DashboardPage.Audio, "Audio"),
            (DashboardPage.Power, "Energy"),
            (DashboardPage.System, "System"),
            (DashboardPage.Optimize, "Optimize"),
            (DashboardPage.Settings, "Settings")
        }.Select(entry => (entry.Item1, L.T(entry.Item2).ToUpper(CultureInfo.CurrentCulture))).ToArray())
        {
            Margin = Padding.Empty,
            Width = SidebarWidth - CardMargin
        };
        _nav.PageRequested += (_, page) => PageRequested?.Invoke(this, page);
        top.Controls.Add(_nav);

        var bottom = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SidebarColor,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            WrapContents = false
        };

        _error = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = SidebarColor,
            Font = SemiBoldFont(13),
            ForeColor = Color.IndianRed,
            Margin = new Padding(Inset, 0, Inset, S(8)),
            Padding = new Padding(Inset, 0, 0, 0),
            Size = new Size(entryWidth, S(20)),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        bottom.Controls.Add(_error);

        _device = new DeviceBadge
        {
            Margin = new Padding(Inset, 0, Inset, 0),
            Size = new Size(entryWidth, S(84))
        };
        bottom.Controls.Add(_device);

        // Dock order: the last added docks first.
        Controls.Add(bottom);
        Controls.Add(top);
    }

    /// <summary>Raised with the section whose entry was clicked.</summary>
    public event EventHandler<DashboardPage>? PageRequested;

    /// <summary>The computer's name at the card's foot.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string DeviceName
    {
        get => _device.Text;
        set
        {
            _device.Text = value;
            _device.AccessibleName = value;
            _device.Invalidate();
        }
    }

    /// <summary>Lights the entry of the section on show.</summary>
    public void Select(DashboardPage page)
    {
        _nav.Select(page);
    }

    /// <summary>Shows an error in red, or with null takes it away; a cut-short text shows whole on hover.</summary>
    public void ShowError(string? text)
    {
        _error.Text = text ?? string.Empty;
        _error.Visible = text is not null;
        _toolTip.SetToolTip(_error, text ?? string.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var card = new RectangleF(CardMargin, CardMargin, Width - CardMargin, Height - 2 * CardMargin);
        using var fill = new SolidBrush(SidebarColor);
        using var path = RoundedButton.RoundedPath(card, CardRadius);
        graphics.FillPath(fill, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();

        base.Dispose(disposing);
    }

    // The app's logo alone, without its name, 12px above the first entry.
    private sealed class BrandRow : Control
    {
        private readonly Image? _logo;

        public BrandRow(Image? logo)
        {
            _logo = logo;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleName = "RazerHelper";
            BackColor = SidebarColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            if (_logo is not null)
                e.Graphics.DrawImage(_logo, S(8), (Height - _logo.Height) / 2, _logo.Width, _logo.Height);
        }
    }

    // A thin line across the top, then a green laptop and, under it, the computer's name, centred.
    private sealed class DeviceBadge : Control
    {
        private static readonly Font NameFont = SemiBoldFont(14);

        // The laptop: a screen and the small stand under it.
        private static Size ScreenSize => new(S(40), S(25));
        private static Size StandSize => new(S(14), S(4));

        public DeviceBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.StaticText;
            BackColor = SidebarColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(BackColor);

            using (var line = new SolidBrush(DividerColor))
                graphics.FillRectangle(line, 0, 0, Width, S(1));

            var iconTop = S(16);
            PaintLaptop(graphics, new PointF(Width / 2f, iconTop));

            var textTop = iconTop + ScreenSize.Height + StandSize.Height + S(8);
            TextRenderer.DrawText(graphics, Text, NameFont, new Rectangle(0, textTop, Width, Height - textTop), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        // A screen outlined in green over a faint green fill, with rounded
        // corners, and a solid green stand centred under it; its top middle at <paramref name="top"/>.
        private static void PaintLaptop(Graphics graphics, PointF top)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var outline = S(1.5f);
            var screen = new RectangleF(top.X - ScreenSize.Width / 2f, top.Y, ScreenSize.Width, ScreenSize.Height);
            var inner = RectangleF.Inflate(screen, -outline / 2, -outline / 2);

            using (var shape = RoundedButton.RoundedPath(inner, S(3f)))
            using (var fill = new SolidBrush(Color.FromArgb(36, RazerGreen)))
            using (var pen = new Pen(RazerGreen, outline))
            {
                graphics.FillPath(fill, shape);
                graphics.DrawPath(pen, shape);
            }

            using var stand = new SolidBrush(RazerGreen);
            graphics.FillRectangle(stand, top.X - StandSize.Width / 2f, screen.Bottom, StandSize.Width, StandSize.Height);
        }
    }
}
