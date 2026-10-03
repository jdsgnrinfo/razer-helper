using System.Drawing.Drawing2D;
using RazerHelper.Core.Localization;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>The main window's sections, in the sidebar's order.</summary>
internal enum DashboardPage
{
    Performance,
    Display,
    Power,
    System,
    Optimize,
    Settings
}

/// <summary>
/// The left of the main window: the app's logo and name, one entry per
/// section (the chosen one lit, with a green edge), then at the bottom Close
/// and the laptop's name and the version. Errors take the laptop's place, in
/// red, until the next result. It only reports clicks; the window decides
/// what they do.
/// </summary>
internal sealed class Sidebar : Panel
{
    public static int SidebarWidth => S(210);

    private static int Inset => S(12);

    private readonly Dictionary<DashboardPage, NavButton> _entries = [];
    private readonly Label _status;
    private readonly ThemedToolTip _toolTip = new();

    public Sidebar(Image? logo)
    {
        BackColor = SidebarColor;
        Dock = DockStyle.Left;
        Margin = Padding.Empty;
        Padding = new Padding(Inset, S(18), Inset, S(14));
        Width = SidebarWidth;

        var entryWidth = SidebarWidth - 2 * Inset;

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

        top.Controls.Add(CreateBrand(logo, entryWidth));

        foreach (var (page, glyph, text) in new[]
        {
            (DashboardPage.Performance, Glyph.Rocket, "Performance"),
            (DashboardPage.Display, Glyph.Display, "Display and lighting"),
            (DashboardPage.Power, Glyph.Battery, "Battery and power"),
            (DashboardPage.System, Glyph.System, "System"),
            (DashboardPage.Optimize, Glyph.Optimize, "Optimize"),
            (DashboardPage.Settings, Glyph.Settings, "Settings")
        })
        {
            var entry = new NavButton(glyph, L.T(text)) { Width = entryWidth, Margin = new Padding(0, 0, 0, S(4)) };
            entry.Click += (_, _) => PageRequested?.Invoke(this, page);
            _entries[page] = entry;
            top.Controls.Add(entry);
        }

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

        // Hides the window to the tray, from any section: the app keeps running.
        var close = new NavButton(Glyph.Close, L.T("Close")) { Width = entryWidth, Margin = Padding.Empty, LineAbove = true };
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        _toolTip.SetToolTip(close, L.T("Close (RazerHelper keeps running in the tray)"));
        bottom.Controls.Add(close);

        _status = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = SidebarColor,
            Font = SemiBoldFont(13),
            ForeColor = SubtleTextColor,
            Margin = new Padding(0, S(10), 0, 0),
            Padding = new Padding(Inset, 0, 0, 0),
            Size = new Size(entryWidth, S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };
        bottom.Controls.Add(_status);

        bottom.Controls.Add(new Label
        {
            AutoSize = false,
            BackColor = SidebarColor,
            Font = DesignFont(13),
            ForeColor = Color.FromArgb(0x6E, 0x6E, 0x6E),
            Margin = Padding.Empty,
            Padding = new Padding(Inset, 0, 0, 0),
            Size = new Size(entryWidth, S(20)),
            Text = AppVersion.Current.ToUpperInvariant(),
            TextAlign = ContentAlignment.MiddleLeft
        });

        // Dock order: the last added docks first.
        Controls.Add(bottom);
        Controls.Add(top);
    }

    /// <summary>Raised with the section whose entry was clicked.</summary>
    public event EventHandler<DashboardPage>? PageRequested;

    /// <summary>Raised by Close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Lights the entry of the section on show.</summary>
    public void Select(DashboardPage page)
    {
        foreach (var (each, entry) in _entries)
            entry.Selected = each == page;
    }

    /// <summary>The laptop's name in grey, or an error in red; a cut-short text shows whole on hover.</summary>
    public void ShowStatus(string text, bool isError)
    {
        _status.ForeColor = isError ? Color.IndianRed : SubtleTextColor;
        _status.Text = text;
        _toolTip.SetToolTip(_status, text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _toolTip.Dispose();

        base.Dispose(disposing);
    }

    private static Control CreateBrand(Image? logo, int width) => new BrandRow(logo)
    {
        Margin = new Padding(0, 0, 0, S(16)),
        Size = new Size(width, S(24))
    };

    // The logo and the app's name beside it, 8px apart, 16px above the first entry.
    private sealed class BrandRow : Control
    {
        private static readonly Font NameFont = SemiBoldTitleFont(16);

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

            var x = S(8);

            if (_logo is not null)
            {
                e.Graphics.DrawImage(_logo, x, (Height - _logo.Height) / 2, _logo.Width, _logo.Height);
                x += _logo.Width + S(8);
            }

            TextRenderer.DrawText(e.Graphics, "RazerHelper", NameFont, new Rectangle(x, 0, Width - x, Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// A section's entry: its icon and name, grey at rest; under the pointer
    /// a dark fill and white text; and when its section is on show, the same
    /// with a green edge on the left.
    /// </summary>
    private sealed class NavButton : Control
    {
        private static int IconSize => S(18);

        private readonly Glyph _glyph;
        private bool _hovered;
        private bool _selected;

        public NavButton(Glyph glyph, string text)
        {
            _glyph = glyph;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleName = text;
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            Font = SemiBoldFont(15);
            Height = S(40);
            Text = text;
        }

        /// <summary>A thin line across the top, setting Close apart from the sections.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool LineAbove { get; init; }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;

                _selected = value;
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(SidebarColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var top = LineAbove ? S(9) : 0;
            var lit = _hovered || _selected;

            if (LineAbove)
            {
                using var line = new SolidBrush(DividerColor);
                graphics.FillRectangle(line, 0, 0, Width, S(1));
            }

            if (lit)
            {
                using var fill = new SolidBrush(ButtonColor);
                using var path = RoundedButton.RoundedPath(new RectangleF(0, top, Width, Height - top), S(RoundedButton.CornerRadius));
                graphics.FillPath(fill, path);
            }

            if (_selected)
            {
                using var edge = new SolidBrush(RazerGreen);
                graphics.FillRectangle(edge, 0, top, S(3), Height - top);
            }

            var color = lit ? Color.White : SubtleTextColor;
            var middle = top + (Height - top) / 2f;
            Glyphs.Draw(graphics, _glyph, new RectangleF(S(12), middle - IconSize / 2f, IconSize, IconSize), color);

            var textLeft = S(12) + IconSize + S(10);
            TextRenderer.DrawText(graphics, Text, Font, new Rectangle(textLeft, top, Width - textLeft, Height - top), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
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

        protected override AccessibleObject CreateAccessibilityInstance() => new NavAccessible(this);

        private sealed class NavAccessible(NavButton owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleStates State =>
                base.State | (owner.Selected ? AccessibleStates.Selected : AccessibleStates.None);

            public override void DoDefaultAction() => owner.OnClick(EventArgs.Empty);
        }
    }
}
