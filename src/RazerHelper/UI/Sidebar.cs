using System.Diagnostics;
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
/// line, Settings. The menu button folds the sidebar down to its icons, on a
/// spring, and opens it again. An error shows in red just above the foot, only until
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

    // Shows the fold as it is now: the width and the names fading.
    private void ApplyFold()
    {
        var folded = (float)Math.Clamp(_fold.Position, 0, 1);
        _nav.Folded = _footNav.Folded = folded;
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

        _footNav.Bounds = new Rectangle(0, Height - S(10) - _footNav.Height, width, _footNav.Height);
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
}
