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
    Power,
    System,
    Optimize,
    Settings
}

/// <summary>
/// The left of the main window: the app's logo and name, one entry per
/// section in capitals (the chosen one green, softly glowing, like the app's
/// other selected buttons), then Close at the bottom.
/// An error shows in red just above Close, only until the next result. It only reports clicks; the window decides
/// what they do.
/// </summary>
internal sealed class Sidebar : Panel
{
    public static int SidebarWidth => S(240);

    private static int Inset => S(12);

    // Each entry keeps this much room around its fill, where the glow shows;
    // with no margin between them, the fills stay 4px apart.
    private static int EntryRoom => S(2);

    private readonly Dictionary<DashboardPage, NavButton> _entries = [];
    private readonly Label _error;
    private readonly ThemedToolTip _toolTip = new();

    public Sidebar(Image? logo)
    {
        BackColor = SidebarColor;
        Dock = DockStyle.Left;
        Margin = Padding.Empty;

        // No side padding, so the selected entry's glow can spread beside it;
        // the controls keep their inset as margins instead.
        Padding = new Padding(0, S(18), 0, S(14));
        Width = SidebarWidth;

        var entryWidth = SidebarWidth - 2 * Inset;
        var aside = new Padding(Inset, 0, Inset, 0);

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

        top.Controls.Add(CreateBrand(logo, entryWidth));

        foreach (var (page, glyph, text) in new[]
        {
            (DashboardPage.Performance, Glyph.Rocket, "Performance"),
            (DashboardPage.Display, Glyph.Display, "Display and lighting"),
            (DashboardPage.Power, Glyph.Battery, "Energy"),
            (DashboardPage.System, Glyph.System, "System"),
            (DashboardPage.Optimize, Glyph.Optimize, "Optimize"),
            (DashboardPage.Settings, Glyph.Settings, "Settings")
        })
        {
            var entry = new NavButton(glyph, L.T(text).ToUpper(CultureInfo.CurrentCulture))
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

        // Hides the window to the tray, from any section: the app keeps running.
        var close = new NavButton(Glyph.Close, L.T("Close")) { Width = entryWidth, Margin = aside, LineAbove = true };
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        _toolTip.SetToolTip(close, L.T("Close (RazerHelper keeps running in the tray)"));
        bottom.Controls.Add(close);

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

    /// <summary>Shows an error in red, or with null takes it away; a cut-short text shows whole on hover.</summary>
    public void ShowError(string? text)
    {
        _error.Text = text ?? string.Empty;
        _error.Visible = text is not null;
        _toolTip.SetToolTip(_error, text ?? string.Empty);
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
            Glow.Paint(graphics, body);
        }
    }

    private static Control CreateBrand(Image? logo, int width) => new BrandRow(logo)
    {
        Margin = new Padding(Inset, 0, Inset, S(16)),
        Size = new Size(width, S(24))
    };

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
    /// a dark fill and white text; and when its section is on show, a green
    /// fill (lighter under the pointer) with dark icon and text, and a soft
    /// green glow around it, as the app's selected buttons have.
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

        /// <summary>Room kept around the fill, where the glow shows.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Room { get; init; }

        /// <summary>The entry itself, inside its room, in its own coordinates.</summary>
        internal RectangleF Body
        {
            get
            {
                var top = LineAbove ? S(9) : Room;
                return new RectangleF(Room, top, Width - 2 * Room, Height - top - Room);
            }
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

            if (LineAbove)
            {
                using var line = new SolidBrush(DividerColor);
                graphics.FillRectangle(line, 0, 0, Width, S(1));
            }

            // The part of the selected entry's glow that falls on this one.
            if (Parent is { } row)
            {
                var state = graphics.Save();
                graphics.TranslateTransform(-Left, -Top);
                PaintGlow(graphics, row);
                graphics.Restore(state);
            }

            var body = Body;

            if (_selected || _hovered)
            {
                var fillColor = !_selected ? ButtonColor : _hovered ? RazerGreenHover : RazerGreen;
                using var fill = new SolidBrush(fillColor);
                using var path = RoundedButton.RoundedPath(body, S(RoundedButton.CornerRadius));
                graphics.FillPath(fill, path);
            }

            var color = _selected ? OnGreenTextColor : _hovered ? Color.White : SubtleTextColor;
            var middle = body.Top + body.Height / 2f;
            var icon = new RectangleF(body.Left + S(12), middle - IconSize / 2f, IconSize, IconSize);
            Glyphs.Draw(graphics, _glyph, icon, color);

            var textLeft = (int)body.Left + S(12) + IconSize + S(10);
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
