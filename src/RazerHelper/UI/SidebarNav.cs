using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>One line of the sidebar's list: a section with its icon, or the name of a group of sections.</summary>
internal sealed record NavEntry(DashboardPage? Page, string Text, NavIcon Icon = NavIcon.Menu)
{
    public static NavEntry Item(DashboardPage page, string text, NavIcon icon) => new(page, text, icon);

    public static NavEntry Group(string text) => new(null, text);

    public bool IsGroup => Page is null;
}

/// <summary>
/// A list of the sidebar's sections, each its icon and its name, under the
/// quiet names of their groups, drawn as one so its two highlights can travel
/// between them. The section on show has a green wash fading to the right,
/// with a lit green bar at its left edge, its icon green and its name white;
/// it glides to another on a spring. A faint highlight follows the pointer (or
/// the keys) from entry to entry: it fades in where it lands when the pointer
/// comes in from outside, travels between entries on a quicker spring, and
/// fades out when the pointer leaves; under it the icon turns green and the
/// name white. A press deepens it. When the section on show is in another
/// list, the wash fades away. Folded (see <see cref="Folded"/>) the names and
/// groups fade, leaving the icons. Up and Down move along the list, Enter or
/// Space opens the entry. With Windows' animation effects off, all of it jumps.
/// </summary>
internal sealed class SidebarNav : Control
{
    /// <summary>One section's height.</summary>
    public static int EntryHeight => S(40);

    // A group's name: the room above its sections, its text at the foot.
    private static int GroupHeight => S(24);

    // The icon's and the name's left edges, and the icon's size.
    private static int IconLeft => S(18);
    private static int IconSize => S(20);
    private static int TextLeft => IconLeft + IconSize + S(14);

    // The lit bar at the left of the section on show.
    private static int BarWidth => S(3);
    private static int BarInset => S(6);

    // The wash's strength at its left edge and at seven tenths across, after which it fades to nothing.
    private const int WashAlpha = 77;
    private const int WashMidAlpha = 15;

    // The highlight's white, at rest and while pressed.
    private const int HoverAlpha = 13;
    private const int PressedAlpha = 24;

    private static readonly Color NameColor = Color.FromArgb(0xCF, 0xCF, 0xCF);
    private static readonly Color IconColor = Color.FromArgb(0xBD, 0xBD, 0xBD);
    private static readonly Color GroupColor = Color.FromArgb(0x58, 0x58, 0x58);

    private static readonly Font NameFont = DesignFont(15);
    private static readonly Font SelectedNameFont = SemiBoldFont(15);
    private static readonly Font GroupFont = DesignFont(12);

    // Quick, with a hint of bounce: the wash settles in about a sixth of a
    // second, the highlight and the press in about a tenth.
    private static readonly Spring.Feel WashFeel = Spring.Feel.Of(0.16, 0.1);
    private static readonly Spring.Feel QuickFeel = Spring.Feel.Of(0.11, 0.05);

    private readonly NavEntry[] _entries;
    private readonly int[] _tops;
    private readonly Spring _wash = new(0, 0.05);
    private readonly Spring _washShown = new(0, 0.002);
    private readonly Spring _highlight = new(0, 0.05);
    private readonly Spring _highlightShown = new(0, 0.002);
    private readonly Spring _pressDepth = new(0, 0.002);

    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;

    // -1 while the section on show is in another list.
    private int _selected = -1;
    private int _hovered = -1;
    private int _pressed = -1;
    // Where the keys are, when they moved last; -1 when the pointer leads.
    private int _keyed = -1;
    private float _folded;

    public SidebarNav(IReadOnlyList<NavEntry> entries)
    {
        _entries = [.. entries];
        _tops = new int[_entries.Length];

        var top = 0;

        for (var index = 0; index < _entries.Length; index++)
        {
            _tops[index] = top;
            top += _entries[index].IsGroup ? GroupHeight : EntryHeight;
        }

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        AccessibleRole = AccessibleRole.List;
        Cursor = Cursors.Hand;
        Height = top;
        TabStop = true;

        _frames.Tick += (_, _) => Frame();
    }

    /// <summary>Raised with the section whose entry was clicked, or opened with the keys.</summary>
    public event EventHandler<DashboardPage>? PageRequested;

    /// <summary>How far the list is folded to its icons: 0 open, 1 folded; the names and groups fade with it.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float Folded
    {
        get => _folded;
        set
        {
            _folded = Math.Clamp(value, 0, 1);
            Invalidate();
        }
    }

    /// <summary>Lights the section on show, gliding there; a section not in this list fades the wash away.</summary>
    public void Select(DashboardPage page)
    {
        var index = Array.FindIndex(_entries, entry => entry.Page == page);
        var animate = IsHandleCreated && Visible;

        if (index < 0)
        {
            _selected = -1;
            Glide(_washShown, 0, QuickFeel, animate);
            StartFrames();
            return;
        }

        // Coming from another list the wash appears in place; within the list it glides.
        if (_selected < 0 || !animate)
            _wash.Jump(_tops[index]);
        else
            Glide(_wash, _tops[index], WashFeel, animate);

        _selected = index;
        Glide(_washShown, 1, QuickFeel, animate);
        AccessibilityNotifyClients(AccessibleEvents.Selection, index);
        StartFrames();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        _keyed = -1;
        HoverOver(EntryAt(e.Y));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (_keyed < 0)
            HoverOver(-1);

        Release();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left && EntryAt(e.Y) is var index and >= 0)
            PressDown(index);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        var pressed = _pressed;
        Release();

        if (e.Button == MouseButtons.Left && pressed >= 0 && EntryAt(e.Y) == pressed)
            Open(pressed);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var items = Enumerable.Range(0, _entries.Length).Where(index => !_entries[index].IsGroup).ToArray();
        var from = _keyed >= 0 ? _keyed : _hovered >= 0 ? _hovered : _selected >= 0 ? _selected : items[0];
        var at = Array.IndexOf(items, from);

        switch (e.KeyCode)
        {
            case Keys.Up or Keys.Down or Keys.Home or Keys.End:
                _keyed = e.KeyCode switch
                {
                    Keys.Up => items[Math.Max(0, at - 1)],
                    Keys.Down => items[Math.Min(items.Length - 1, at + 1)],
                    Keys.Home => items[0],
                    _ => items[^1]
                };
                HoverOver(_keyed);
                e.Handled = true;
                break;
            case Keys.Enter or Keys.Space:
                _keyed = from;
                HoverOver(from);
                PressDown(from);
                e.Handled = true;
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.KeyCode is Keys.Enter or Keys.Space && _pressed >= 0)
        {
            var pressed = _pressed;
            Release();
            Open(pressed);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        // Reached with Tab: the highlight starts on the section on show, or the first.
        if (ShowFocusCues && _hovered < 0)
        {
            _keyed = _selected >= 0 ? _selected : Array.FindIndex(_entries, entry => !entry.IsGroup);
            HoverOver(_keyed);
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);

        if (_keyed >= 0)
        {
            _keyed = -1;
            HoverOver(-1);
        }

        Release();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // The highlight under the pointer, beneath the wash.
        var shown = (float)Math.Clamp(_highlightShown.Position, 0, 1);

        if (shown > 0)
        {
            var alpha = HoverAlpha + (PressedAlpha - HoverAlpha) * (float)Math.Clamp(_pressDepth.Position, 0, 1);
            using var tint = new SolidBrush(Color.FromArgb((int)(alpha * shown), Color.White));
            graphics.FillRectangle(tint, Body((float)_highlight.Position));
        }

        var washShown = (float)Math.Clamp(_washShown.Position, 0, 1);

        if (washShown > 0)
            PaintWash(graphics, Body((float)_wash.Position), washShown);

        for (var index = 0; index < _entries.Length; index++)
        {
            var entry = _entries[index];
            var body = new Rectangle(0, _tops[index], Width, entry.IsGroup ? GroupHeight : EntryHeight);

            if (entry.IsGroup)
            {
                PaintName(graphics, entry.Text, GroupFont, GroupColor, new Rectangle(IconLeft, body.Top, Width - IconLeft, body.Height - S(2)), TextFormatFlags.Bottom);
                continue;
            }

            var lit = index == _selected || index == _hovered && shown > 0.5f;
            var icon = new RectangleF(IconLeft, body.Top + (body.Height - IconSize) / 2f, IconSize, IconSize);
            NavIcons.Draw(graphics, entry.Icon, icon, lit ? RazerGreen : IconColor);

            PaintName(graphics, entry.Text, index == _selected ? SelectedNameFont : NameFont, lit ? Color.White : NameColor,
                new Rectangle(TextLeft, body.Top, Width - TextLeft, body.Height), TextFormatFlags.VerticalCenter);
        }

        // Only keyboard focus shows a ring, round the entry the keys are on.
        if (Focused && ShowFocusCues && _keyed >= 0)
        {
            using var ring = new Pen(RazerGreen, S(1.5f));
            var body = Body((float)_highlight.Position);
            graphics.DrawRectangle(ring, body.X + S(2), body.Y + S(1), body.Width - S(4), body.Height - S(2));
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new NavAccessible(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    // The green wash, strongest at the left and gone by the right edge, and the lit bar at its left.
    private static void PaintWash(Graphics graphics, RectangleF body, float shown)
    {
        using (var wash = new LinearGradientBrush(new RectangleF(body.Left - 1, body.Top, body.Width + 2, body.Height), Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
        {
            wash.InterpolationColors = new ColorBlend
            {
                Colors =
                [
                    Color.FromArgb((int)(WashAlpha * shown), RazerGreen),
                    Color.FromArgb((int)(WashMidAlpha * shown), RazerGreen),
                    Color.FromArgb(0, RazerGreen)
                ],
                Positions = [0f, 0.7f, 1f]
            };
            graphics.FillRectangle(wash, body);
        }

        var bar = new RectangleF(body.Left, body.Top + BarInset, BarWidth, body.Height - 2 * BarInset);

        // Its light: a few faint layers spreading from the bar.
        for (var layer = 4; layer >= 1; layer--)
        {
            using var light = new SolidBrush(Color.FromArgb((int)(18 * shown), RazerGreen));
            graphics.FillRectangle(light, RectangleF.Inflate(bar, S(layer * 1.5f), S(layer * 1.5f)));
        }

        using var fill = new SolidBrush(Color.FromArgb((int)(255 * shown), RazerGreen));
        graphics.FillRectangle(fill, bar);
    }

    // A name fading out as the list folds.
    private void PaintName(Graphics graphics, string text, Font font, Color color, Rectangle bounds, TextFormatFlags place)
    {
        if (_folded >= 1)
            return;

        TextRenderer.DrawText(graphics, text, font, bounds, Motion.Blend(color, BackColor, _folded),
            place | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    // Moves the highlight to an entry, or with -1 lets it fade where it is.
    private void HoverOver(int index)
    {
        if (index == _hovered)
            return;

        var wasShown = _hovered >= 0;
        _hovered = index;

        if (index < 0)
        {
            Glide(_highlightShown, 0, QuickFeel);
        }
        else
        {
            // Coming in from outside it appears where it lands; between entries it travels.
            if (!wasShown && _highlightShown.Position < 0.05)
                _highlight.Jump(_tops[index]);
            else
                Glide(_highlight, _tops[index], QuickFeel);

            Glide(_highlightShown, 1, QuickFeel);
        }

        StartFrames();
    }

    private void PressDown(int index)
    {
        _pressed = index;
        Glide(_pressDepth, 1, QuickFeel);
        StartFrames();
    }

    private void Release()
    {
        if (_pressed < 0)
            return;

        _pressed = -1;
        Glide(_pressDepth, 0, QuickFeel);
        StartFrames();
    }

    private void Open(int index)
    {
        if (_entries[index].Page is { } page)
            PageRequested?.Invoke(this, page);
    }

    private RectangleF Body(float top) => new(0, top, Width, EntryHeight);

    // The section under <paramref name="y"/>; -1 over a group's name or past the list.
    private int EntryAt(int y)
    {
        for (var index = 0; index < _entries.Length; index++)
        {
            if (!_entries[index].IsGroup && y >= _tops[index] && y < _tops[index] + EntryHeight)
                return index;
        }

        return -1;
    }

    private static void Glide(Spring spring, double target, Spring.Feel feel, bool animate = true)
    {
        if (Motion.Reduced || !animate)
            spring.Jump(target);
        else
            spring.To(target, feel);
    }

    private void StartFrames()
    {
        if (!_frames.Enabled)
        {
            _lastFrame = _clock.Elapsed.TotalSeconds;
            _frames.Start();
        }

        Invalidate();
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        var moving = _wash.Step(seconds) | _washShown.Step(seconds) | _highlight.Step(seconds) | _highlightShown.Step(seconds) | _pressDepth.Step(seconds);
        Invalidate();

        if (!moving)
            _frames.Stop();
    }

    // The list, and an item per section, for screen readers.
    private sealed class NavAccessible(SidebarNav owner) : ControlAccessibleObject(owner)
    {
        private int[] Items => [.. Enumerable.Range(0, owner._entries.Length).Where(index => !owner._entries[index].IsGroup)];

        public override int GetChildCount() => Items.Length;

        public override AccessibleObject? GetChild(int index) =>
            index >= 0 && index < Items.Length ? new EntryAccessible(owner, this, Items[index]) : null;
    }

    private sealed class EntryAccessible(SidebarNav owner, AccessibleObject list, int index) : AccessibleObject
    {
        public override string? Name => owner._entries[index].Text;

        public override AccessibleRole Role => AccessibleRole.ListItem;

        public override AccessibleObject? Parent => list;

        public override AccessibleStates State =>
            AccessibleStates.Selectable | AccessibleStates.Focusable | (index == owner._selected ? AccessibleStates.Selected : AccessibleStates.None);

        public override string DefaultAction => "Open";

        public override Rectangle Bounds => owner.RectangleToScreen(Rectangle.Round(owner.Body(owner._tops[index])));

        public override void DoDefaultAction() => owner.Open(index);
    }
}
