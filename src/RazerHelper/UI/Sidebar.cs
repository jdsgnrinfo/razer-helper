using System.Diagnostics;
using System.Drawing.Drawing2D;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>The main window's sections, in the sidebar's order.</summary>
internal enum DashboardPage
{
    Performance,
    Lighting,
    Display,
    Audio,
    Power,
    Optimize,
    System,
    Settings
}

/// <summary>
/// The left of the main window, a step darker than the pages beside it with
/// a faint line between: the menu button, then the sections with their icons
/// under the names of their groups (see SidebarNav) and, at the foot, past a
/// line, Settings and this computer, its laptop drawn in fine lines beside its
/// name. The menu button folds the sidebar down to its icons, on a spring,
/// and opens it again. An error shows in red just above the foot, only until
/// the next result. It only reports clicks and the width it wants; the window
/// decides what they do.
/// </summary>
internal sealed class Sidebar : Panel
{
    /// <summary>The sidebar's width open.</summary>
    public static int OpenWidth => S(212);

    /// <summary>The sidebar's width folded to its icons.</summary>
    public static int FoldedWidth => S(56);

    /// <summary>The sidebar's background.</summary>
    public static readonly Color NavColor = Color.FromArgb(0x15, 0x15, 0x15);

    private static readonly Color EdgeColor = Color.FromArgb(0x1B, 0x1B, 0x1B);
    private static readonly Color FootLineColor = Color.FromArgb(0x22, 0x22, 0x22);

    // A little quicker than the sections' wash, with a hint of bounce.
    private static readonly Spring.Feel FoldFeel = Spring.Feel.Of(0.22, 0.12);

    private readonly MenuButton _menu = new();
    private readonly SidebarNav _nav;
    private readonly SidebarNav _footNav;
    private readonly Label _error;
    private readonly DeviceBadge _device = new();
    private readonly ThemedToolTip _toolTip = new();

    private readonly Spring _fold = new(0, 0.002);
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private bool _collapsed;

    public Sidebar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = NavColor;
        Dock = DockStyle.Left;
        Margin = Padding.Empty;
        Width = OpenWidth;

        _menu.Click += (_, _) => Collapsed = !Collapsed;
        Controls.Add(_menu);

        _nav = new SidebarNav(
        [
            NavEntry.Item(DashboardPage.Performance, L.T("Performance"), NavIcon.Performance),
            NavEntry.Group(L.T("Device")),
            NavEntry.Item(DashboardPage.Lighting, L.T("Lighting"), NavIcon.Lighting),
            NavEntry.Item(DashboardPage.Display, L.T("Display"), NavIcon.Display),
            NavEntry.Item(DashboardPage.Audio, L.T("Audio"), NavIcon.Audio),
            NavEntry.Group(L.T("Assistant")),
            NavEntry.Item(DashboardPage.Power, L.T("Power profiles"), NavIcon.PowerProfiles),
            NavEntry.Item(DashboardPage.Optimize, L.T("Optimize"), NavIcon.Optimize),
            NavEntry.Item(DashboardPage.System, L.T("System"), NavIcon.System)
        ])
        { BackColor = NavColor };

        _footNav = new SidebarNav([NavEntry.Item(DashboardPage.Settings, L.T("Settings"), NavIcon.Settings)]) { BackColor = NavColor };

        foreach (var nav in new[] { _nav, _footNav })
        {
            nav.PageRequested += (_, page) => PageRequested?.Invoke(this, page);
            Controls.Add(nav);
        }

        _error = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = NavColor,
            Font = SemiBoldFont(13),
            ForeColor = Color.IndianRed,
            Padding = new Padding(S(18), 0, S(8), 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        Controls.Add(_error);
        Controls.Add(_device);

        _frames.Tick += (_, _) => Frame();
        Layout += (_, _) => Arrange();
        UpdateMenuTip();
    }

    /// <summary>Raised with the section whose entry was clicked.</summary>
    public event EventHandler<DashboardPage>? PageRequested;

    /// <summary>
    /// Raised on every step of folding or opening, with the width the sidebar
    /// wants now; whoever handles it sets the width (the window grows or
    /// shrinks along with it). Unhandled, the sidebar sets its own.
    /// </summary>
    public event EventHandler<int>? WidthWanted;

    /// <summary>Raised when the menu button folds or opens the sidebar.</summary>
    public event EventHandler<bool>? CollapsedChanged;

    /// <summary>Folded down to its icons. Setting it from the menu button animates; see <see cref="SetCollapsed"/>.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            if (_collapsed == value)
                return;

            SetCollapsed(value, animate: IsHandleCreated && Visible && !Motion.Reduced);
            CollapsedChanged?.Invoke(this, value);
        }
    }

    /// <summary>The width for the sidebar as it is now.</summary>
    public int CurrentWidth => (int)Math.Round(OpenWidth + (FoldedWidth - OpenWidth) * _fold.Position);

    /// <summary>Folds or opens the sidebar, gliding there or at once.</summary>
    public void SetCollapsed(bool collapsed, bool animate)
    {
        _collapsed = collapsed;
        UpdateMenuTip();

        if (animate)
        {
            _fold.To(collapsed ? 1 : 0, FoldFeel);

            if (!_frames.Enabled)
            {
                _lastFrame = _clock.Elapsed.TotalSeconds;
                _frames.Start();
            }
        }
        else
        {
            _fold.Jump(collapsed ? 1 : 0);
            ApplyFold();
        }
    }

    /// <summary>The computer's name at the foot.</summary>
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

    /// <summary>Whether the laptop's own controls were found: "Connected" under its name, or "Not detected".</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool DeviceConnected
    {
        get => _device.Connected;
        set
        {
            _device.Connected = value;
            _device.Invalidate();
        }
    }

    /// <summary>Lights the entry of the section on show.</summary>
    public void Select(DashboardPage page)
    {
        _nav.Select(page);
        _footNav.Select(page);
    }

    /// <summary>Shows an error in red, or with null takes it away; a cut-short text shows whole on hover.</summary>
    public void ShowError(string? text)
    {
        _error.Text = text ?? string.Empty;
        _error.Visible = text is not null && _fold.Position < 0.5;
        _toolTip.SetToolTip(_error, text ?? string.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);

        using (var edge = new SolidBrush(EdgeColor))
            graphics.FillRectangle(edge, Width - S(1), 0, S(1), Height);

        using var line = new SolidBrush(FootLineColor);
        graphics.FillRectangle(line, 0, _footNav.Top - S(7), Width - S(1), S(1));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frames.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        if (!_fold.Step(seconds))
            _frames.Stop();

        ApplyFold();
    }

    // Shows the fold as it is now: the width, the names fading, the computer shrinking.
    private void ApplyFold()
    {
        var folded = (float)Math.Clamp(_fold.Position, 0, 1);
        _nav.Folded = _footNav.Folded = folded;
        _device.Folded = folded;
        _error.Visible = _error.Text.Length > 0 && folded < 0.5f;

        if (WidthWanted is { } handler)
            handler(this, CurrentWidth);
        else
            Width = CurrentWidth;
    }

    private void UpdateMenuTip()
    {
        var tip = L.T(_collapsed ? "Expand menu" : "Collapse menu");
        _menu.AccessibleName = tip;
        _toolTip.SetToolTip(_menu, tip);
    }

    // The parts placed for the sidebar's width: as wide as it, short of its edge line.
    private void Arrange()
    {
        var width = Width - S(1);

        _menu.Bounds = new Rectangle(0, S(6), FoldedWidth, S(38));
        _nav.Bounds = new Rectangle(0, _menu.Bottom, width, _nav.Height);

        var deviceHeight = S(12 + 31 + 4);
        _device.Bounds = new Rectangle(0, Height - S(10) - deviceHeight, width, deviceHeight);
        _footNav.Bounds = new Rectangle(0, _device.Top - _footNav.Height, width, _footNav.Height);
        _error.Bounds = new Rectangle(0, _footNav.Top - S(7) - S(8) - S(20), width, S(20));
        Invalidate();
    }

    // The three lines that fold and open the sidebar: grey, green under the pointer.
    private sealed class MenuButton : Control
    {
        private bool _hovered;

        public MenuButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.PushButton;
            BackColor = NavColor;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            var size = S(20f);
            NavIcons.Draw(e.Graphics, NavIcon.Menu, new RectangleF((Width - size) / 2, (Height - size) / 2, size, size),
                _hovered ? RazerGreen : Color.FromArgb(0xBB, 0xBB, 0xBB));
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

    // This computer: its laptop in fine lines, then its name and whether it was found, beside it.
    private sealed class DeviceBadge : Control
    {
        private static readonly Font NameFont = SemiBoldFont(14);
        private static readonly Font StateFont = DesignFont(13);

        // The drawing's own grid, 300 by 190.
        private static readonly SizeF Art = new(300, 190);

        public DeviceBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.StaticText;
            BackColor = NavColor;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Connected { get; set; } = true;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public float Folded { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(BackColor);

            // 48 by 31 open, 40 by 26 folded, nearer the edge.
            var width = S(48f + (40 - 48) * Folded);
            var height = S(31f + (26 - 31) * Folded);
            var left = S(14f + (4 - 14) * Folded);
            var top = S(12f);
            PaintLaptop(graphics, new RectangleF(left, top + (S(31f) - height) / 2, width, height));

            if (Folded >= 1)
                return;

            var textLeft = (int)(left + width + S(12));
            var bounds = new Rectangle(textLeft, (int)top - S(2), Width - textLeft - S(8), S(35));
            var nameHeight = TextRenderer.MeasureText(graphics, "Ag", NameFont, Size.Empty, TextFormatFlags.NoPadding).Height;

            TextRenderer.DrawText(graphics, Text, NameFont, bounds, Motion.Blend(Color.White, BackColor, Folded),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(graphics, L.T(Connected ? "Connected" : "Not detected"), StateFont,
                new Rectangle(bounds.Left, bounds.Top + nameHeight + S(1), bounds.Width, bounds.Height - nameHeight),
                Motion.Blend(SubtleTextColor, BackColor, Folded),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        // The laptop open, seen a little from above, in fine light lines: the
        // lid with its screen in green, the deck with rows of keys, the
        // touchpad and the green light at the front of the keyboard.
        private static void PaintLaptop(Graphics graphics, RectangleF bounds)
        {
            var state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / Art.Width, bounds.Height / Art.Height);

            // The lines keep their width in the window's pixels, whatever the drawing's size.
            var unit = Art.Width / bounds.Width;

            using (var lid = SvgPath.Parse("M62 14h176a6 6 0 0 1 6 6v112H56V20a6 6 0 0 1 6-6z"))
            using (var pen = new Pen(Color.FromArgb(0xD8, 0xD8, 0xD8), S(1.1f) * unit) { LineJoin = LineJoin.Round })
                graphics.DrawPath(pen, lid);

            using (var screen = SvgPath.Parse("M70 26h160v94H70z"))
            using (var glow = new SolidBrush(Color.FromArgb(20, RazerGreen)))
            using (var pen = new Pen(Color.FromArgb(178, RazerGreen), S(1.1f) * unit))
            {
                graphics.FillPath(glow, screen);
                graphics.DrawPath(pen, screen);
            }

            using (var deck = SvgPath.Parse("M56 132h188l38 40H18z"))
            using (var pen = new Pen(Color.FromArgb(0xD8, 0xD8, 0xD8), S(1.1f) * unit) { LineJoin = LineJoin.Round })
                graphics.DrawPath(pen, deck);

            using (var keys = SvgPath.Parse("M66 138h168l6 7H60zM60 145h180l6 7H54zM54 152h192l5 6H49zM92 138l-4 20M118 138l-3 20M144 138l-1 20M170 138v20M196 138l2 20M222 138l3 20"))
            using (var pen = new Pen(Color.FromArgb(0x4A, 0x4A, 0x4A), S(0.8f) * unit))
                graphics.DrawPath(pen, keys);

            using (var pad = SvgPath.Parse("M126 160h48l3 7h-54z"))
            using (var pen = new Pen(Color.FromArgb(0x66, 0x66, 0x66), S(0.8f) * unit))
                graphics.DrawPath(pen, pad);

            using (var light = new Pen(RazerGreen, S(1.5f) * unit))
                graphics.DrawLine(light, 138, 140, 162, 140);

            using (var mark = SvgPath.Parse("M138 66l12-14 12 14-12 14z"))
            using (var pen = new Pen(Color.FromArgb(204, RazerGreen), S(1.1f) * unit))
                graphics.DrawPath(pen, mark);

            graphics.Restore(state);
        }
    }
}
