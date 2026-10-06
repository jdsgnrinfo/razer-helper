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
/// capitals (the chosen one green, with a soft glow) and, at its foot, this
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

    // Each entry keeps this much room around its fill, where the glow shows;
    // with no margin between them, the fills stay 4px apart.
    private static int EntryRoom => S(2);

    // The selected entry's glow, a little softer than the buttons' own.
    private const float GlowStrength = 0.7f;

    private readonly Dictionary<DashboardPage, NavButton> _entries = [];
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

        var top = new GlowRow
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
            Margin = new Padding(Inset, 0, Inset, S(12)),
            Size = new Size(entryWidth, S(40))
        });

        foreach (var (page, text) in new[]
        {
            (DashboardPage.Performance, "Performance"),
            (DashboardPage.Display, "Display and lighting"),
            (DashboardPage.Audio, "Audio"),
            (DashboardPage.Power, "Energy"),
            (DashboardPage.System, "System"),
            (DashboardPage.Optimize, "Optimize"),
            (DashboardPage.Settings, "Settings")
        })
        {
            var entry = new NavButton(L.T(text).ToUpper(CultureInfo.CurrentCulture))
            {
                Font = SemiBoldFont(14),
                Height = S(44) + 2 * EntryRoom,
                Margin = new Padding(Inset - EntryRoom, 0, Inset - EntryRoom, 0),
                Room = EntryRoom,
                Width = entryWidth + 2 * EntryRoom
            };
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
        foreach (var (each, entry) in _entries)
            entry.Selected = each == page;
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

    // Paints, in the row's coordinates, the glow around its selected entry.
    private static void PaintGlow(Graphics graphics, Control row)
    {
        foreach (var entry in row.Controls.OfType<NavButton>())
        {
            if (!entry.Selected)
                continue;

            var body = entry.Body;
            body.Offset(entry.Left, entry.Top);
            Glow.Paint(graphics, body, GlowStrength);
        }
    }

    // The entries' column: it paints the selected entry's glow over the room
    // around it (each entry paints the part that falls on itself).
    private sealed class GlowRow : FlowLayoutPanel
    {
        public GlowRow() => DoubleBuffered = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PaintGlow(e.Graphics, this);
        }
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

        private static int IconSize => S(30);

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
            Glyphs.Draw(graphics, Glyph.Laptop, new RectangleF((Width - IconSize) / 2f, iconTop, IconSize, IconSize), RazerGreen);

            var textTop = iconTop + IconSize + S(6);
            TextRenderer.DrawText(graphics, Text, NameFont, new Rectangle(0, textTop, Width, Height - textTop), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// An entry: its name, grey at rest, green under the pointer, with no
    /// fill; and when its section is on show, a green fill (lighter under the
    /// pointer) with dark text and a soft green glow around it.
    /// </summary>
    private sealed class NavButton : Control
    {
        private bool _hovered;
        private bool _selected;

        public NavButton(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AccessibleName = text;
            AccessibleRole = AccessibleRole.PushButton;
            Cursor = Cursors.Hand;
            Font = SemiBoldFont(15);
            Height = S(40);
            Text = text;
        }

        /// <summary>Room kept around the fill, where the glow shows.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Room { get; init; }

        /// <summary>The entry itself, inside its room, in its own coordinates.</summary>
        internal RectangleF Body => new(Room, Room, Width - 2 * Room, Height - 2 * Room);

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

                // The glow reaches over the neighbours too.
                if (Parent is { } row)
                    row.Invalidate(true);
                else
                    Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(SidebarColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // The part of the selected entry's glow that falls on this one.
            if (Parent is { } row)
            {
                var state = graphics.Save();
                graphics.TranslateTransform(-Left, -Top);
                PaintGlow(graphics, row);
                graphics.Restore(state);
            }

            var body = Body;

            if (_selected)
            {
                var fillColor = _hovered ? RazerGreenHover : RazerGreen;
                using var fill = new SolidBrush(fillColor);
                using var path = RoundedButton.RoundedPath(body, S(RoundedButton.CornerRadius));
                graphics.FillPath(fill, path);
            }

            var color = _selected ? OnGreenTextColor : _hovered ? RazerGreen : SubtleTextColor;
            var textLeft = (int)body.Left + S(16);

            TextRenderer.DrawText(graphics, Text, Font, new Rectangle(textLeft, (int)body.Top, (int)body.Right - textLeft, (int)body.Height), color,
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
