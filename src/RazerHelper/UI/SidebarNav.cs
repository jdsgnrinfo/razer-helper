using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The sidebar's sections, drawn as one list so their two highlights can
/// travel between them. A green pill, with a soft glow, marks the section on
/// show and glides to another on a spring when it changes. A faint green
/// highlight follows the pointer (or the keys) from entry to entry: it fades
/// in where it lands when the pointer comes in from outside, travels between
/// entries on a quicker spring, and fades out when the pointer leaves. An
/// entry's name turns green under the highlight and dark where the pill
/// covers it, changing right at their edges. A press dips the name a touch and
/// deepens the highlight. Up and Down move along the list, Enter or Space
/// opens the entry. With Windows' animation effects off, all of it jumps.
/// </summary>
internal sealed class SidebarNav : Control
{
    /// <summary>One entry's fill height.</summary>
    public static int EntryHeight => S(44);

    // From one entry's top to the next one's: the fills stay 4px apart.
    private static int Pitch => S(48);

    // The room around the list where the pill's glow shows.
    private static int Room => Glow.Radius;

    // The selected entry's glow, a little softer than the buttons' own.
    private const float GlowStrength = 0.7f;

    // The highlight's strength at rest and while pressed.
    private const int HoverAlpha = 26;
    private const int PressedAlpha = 46;

    private const double PressedScale = 0.97;

    // Quicker than the sliders: the pill settles in about a sixth of a second
    // with a hint of bounce, the highlight and the press in about a tenth.
    private static readonly Spring.Feel PillFeel = Spring.Feel.Of(0.16, 0.1);
    private static readonly Spring.Feel QuickFeel = Spring.Feel.Of(0.11, 0.05);

    private readonly (DashboardPage Page, string Text)[] _entries;
    private readonly Spring _pill = new(0, 0.05);
    private readonly Spring _highlight = new(0, 0.05);
    private readonly Spring _highlightShown = new(0, 0.002);
    private readonly Spring _press = new(1, 0.0005);
    private readonly Spring _pressDepth = new(0, 0.002);

    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;

    private int _selected;
    private int _hovered = -1;
    private int _pressed = -1;
    // Where the keys are, when they moved last; -1 when the pointer leads.
    private int _keyed = -1;

    public SidebarNav(IReadOnlyList<(DashboardPage Page, string Text)> entries)
    {
        _entries = [.. entries];

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        AccessibleRole = AccessibleRole.List;
        BackColor = SidebarColor;
        Cursor = Cursors.Hand;
        Font = SemiBoldFont(14);
        Height = 2 * Room + (_entries.Length - 1) * Pitch + EntryHeight;
        TabStop = true;

        _frames.Tick += (_, _) => Frame();
    }

    /// <summary>Raised with the section whose entry was clicked, or opened with the keys.</summary>
    public event EventHandler<DashboardPage>? PageRequested;

    /// <summary>The section on show.</summary>
    public DashboardPage Selected => _entries[_selected].Page;

    /// <summary>Lights the entry of the section on show; on screen the pill glides there.</summary>
    public void Select(DashboardPage page)
    {
        var index = Array.FindIndex(_entries, entry => entry.Page == page);

        if (index < 0 || index == _selected && _pill.Target == TopOf(index))
            return;

        _selected = index;
        AccessibilityNotifyClients(AccessibleEvents.Selection, index);

        if (IsHandleCreated && Visible)
            Animate(_pill, TopOf(index), PillFeel);
        else
            _pill.Jump(TopOf(index));

        StartFrames();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _pill.Jump(TopOf(_selected));
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

        var from = _keyed >= 0 ? _keyed : _hovered >= 0 ? _hovered : _selected;

        switch (e.KeyCode)
        {
            case Keys.Up or Keys.Down or Keys.Home or Keys.End:
                _keyed = e.KeyCode switch
                {
                    Keys.Up => Math.Max(0, from - 1),
                    Keys.Down => Math.Min(_entries.Length - 1, from + 1),
                    Keys.Home => 0,
                    _ => _entries.Length - 1
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

        // Reached with Tab: the highlight starts on the section on show.
        if (ShowFocusCues && _hovered < 0)
        {
            _keyed = _selected;
            HoverOver(_selected);
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

        var corner = S(RoundedButton.CornerRadius);

        // The highlight under the pointer, beneath the pill.
        var shown = (float)Math.Clamp(_highlightShown.Position, 0, 1);

        if (shown > 0)
        {
            var alpha = HoverAlpha + (PressedAlpha - HoverAlpha) * (float)Math.Clamp(_pressDepth.Position, 0, 1);
            using var tint = new SolidBrush(Color.FromArgb((int)(alpha * shown), RazerGreen));
            using var path = RoundedButton.RoundedPath(Body((float)_highlight.Position), corner);
            graphics.FillPath(tint, path);
        }

        // The pill, with its glow.
        var pill = Body((float)_pill.Position);
        Glow.Paint(graphics, pill, GlowStrength);

        using (var fill = new SolidBrush(_hovered == _selected && !_pill.Moving ? RazerGreenHover : RazerGreen))
        using (var path = RoundedButton.RoundedPath(pill, corner))
            graphics.FillPath(fill, path);

        // The names in three colors, each kept to its own part of the list: dark
        // inside the pill, green inside the highlight, grey elsewhere. So a
        // name the pill is passing over changes color right at its edge.
        using var pillShape = RoundedButton.RoundedPath(pill, corner);
        using var highlightShape = RoundedButton.RoundedPath(Body((float)_highlight.Position), corner);

        using (var rest = new Region(ClientRectangle))
        {
            rest.Exclude(pillShape);

            if (shown > 0)
                rest.Exclude(highlightShape);

            PaintNames(graphics, rest, SubtleTextColor);
        }

        if (shown > 0)
        {
            using var lit = new Region(highlightShape);
            lit.Exclude(pillShape);
            PaintNames(graphics, lit, Motion.Blend(SubtleTextColor, RazerGreen, shown));
        }

        using (var covered = new Region(pillShape))
            PaintNames(graphics, covered, OnGreenTextColor);

        // Only keyboard focus shows a ring, round the entry the keys are on.
        if (Focused && ShowFocusCues && _keyed >= 0)
        {
            using var ring = new Pen(RazerGreen, S(1.5f));
            using var path = RoundedButton.RoundedPath(RectangleF.Inflate(Body((float)_highlight.Position), S(2), S(2)), corner + S(2));
            graphics.DrawPath(ring, path);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new NavAccessible(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    // Every name, in one color, inside <paramref name="clip"/>. A pressed name
    // dips: it draws a touch in from its edge, as if shrunk to 97%.
    private void PaintNames(Graphics graphics, Region clip, Color color)
    {
        var state = graphics.Save();
        graphics.SetClip(clip, CombineMode.Intersect);

        for (var index = 0; index < _entries.Length; index++)
        {
            var body = Body(TopOf(index));
            var left = body.Left + S(16);

            if (index == _pressed || _press.Moving && index == _lastPressed)
                left += (float)(1 - _press.Position) * (body.Width / 2 - S(16));

            TextRenderer.DrawText(graphics, _entries[index].Text, Font, new Rectangle((int)Math.Round(left), (int)body.Top, (int)(body.Right - left), (int)body.Height), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
        }

        graphics.Restore(state);
    }

    private int _lastPressed = -1;

    // Moves the highlight to an entry, or with -1 lets it fade where it is.
    private void HoverOver(int index)
    {
        if (index == _hovered)
            return;

        var wasShown = _hovered >= 0;
        _hovered = index;

        if (index < 0)
        {
            Animate(_highlightShown, 0, QuickFeel);
        }
        else
        {
            // Coming in from outside it appears where it lands; between entries it travels.
            if (!wasShown && _highlightShown.Position < 0.05)
                _highlight.Jump(TopOf(index));
            else
                Animate(_highlight, TopOf(index), QuickFeel);

            Animate(_highlightShown, 1, QuickFeel);
        }

        StartFrames();
    }

    private void PressDown(int index)
    {
        _pressed = _lastPressed = index;
        Animate(_press, PressedScale, QuickFeel);
        Animate(_pressDepth, 1, QuickFeel);
        StartFrames();
    }

    private void Release()
    {
        if (_pressed < 0)
            return;

        _pressed = -1;
        Animate(_press, 1, QuickFeel);
        Animate(_pressDepth, 0, QuickFeel);
        StartFrames();
    }

    private void Open(int index) => PageRequested?.Invoke(this, _entries[index].Page);

    private RectangleF Body(float top) => new(Room, top, Width - 2 * Room, EntryHeight);

    private float TopOf(int index) => Room + index * Pitch;

    private int EntryAt(int y)
    {
        var index = (int)Math.Floor((y - Room + (Pitch - EntryHeight) / 2f) / Pitch);
        return index >= 0 && index < _entries.Length ? index : -1;
    }

    private static void Animate(Spring spring, double target, Spring.Feel feel)
    {
        if (Motion.Reduced)
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

        var moving = _pill.Step(seconds) | _highlight.Step(seconds) | _highlightShown.Step(seconds) | _press.Step(seconds) | _pressDepth.Step(seconds);
        Invalidate();

        if (!moving)
            _frames.Stop();
    }

    // The list, and an item per entry, for screen readers.
    private sealed class NavAccessible(SidebarNav owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount() => owner._entries.Length;

        public override AccessibleObject? GetChild(int index) =>
            index >= 0 && index < owner._entries.Length ? new EntryAccessible(owner, this, index) : null;
    }

    private sealed class EntryAccessible(SidebarNav owner, AccessibleObject list, int index) : AccessibleObject
    {
        public override string? Name => owner._entries[index].Text;

        public override AccessibleRole Role => AccessibleRole.ListItem;

        public override AccessibleObject? Parent => list;

        public override AccessibleStates State =>
            AccessibleStates.Selectable | AccessibleStates.Focusable | (index == owner._selected ? AccessibleStates.Selected : AccessibleStates.None);

        public override string DefaultAction => "Open";

        public override Rectangle Bounds => owner.RectangleToScreen(Rectangle.Round(owner.Body(owner.TopOf(index))));

        public override void DoDefaultAction() => owner.Open(index);
    }
}
