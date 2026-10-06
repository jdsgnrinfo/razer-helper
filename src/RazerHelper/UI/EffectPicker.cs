using System.Diagnostics;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>A lighting effect's picture.</summary>
internal enum EffectIcon
{
    Static,
    Breathing,
    Spectrum,
    Wave,
    Off
}

/// <summary>
/// A row of lighting effects, each its picture over its name, in cells of
/// a fixed width so rows of different lengths line up. The chosen one is
/// lit green with a soft glow and its name white, and answers with a little
/// bounce; under the pointer a picture turns white. Left and Right move the
/// choice, as a list's keys do. It only reports the choice; whoever owns it
/// applies it and shows what the laptop then reports with <see cref="Select"/>.
/// </summary>
internal sealed class EffectPicker : Control
{
    /// <summary>The picker's height: the pictures, the gap and the names.</summary>
    public static int PickerHeight => S(66);

    private static int IconSize => S(30);
    private static readonly Font NameFont = DesignFont(13);
    private static readonly Color RestColor = Color.FromArgb(0xCF, 0xCF, 0xCF);
    private static readonly Color NameColor = Color.FromArgb(0xBB, 0xBB, 0xBB);

    // The chosen one's bounce: a touch smaller, then back with a hint past.
    private static readonly Spring.Feel BounceFeel = Spring.Feel.Of(0.32, 0.3);

    private static readonly Dictionary<EffectIcon, GraphicsPath> Strokes = new()
    {
        // A ring round a dot.
        [EffectIcon.Static] = SvgPath.Parse("M4 18a14 14 0 1 0 28 0a14 14 0 1 0 -28 0z"),
        // A pulse.
        [EffectIcon.Breathing] = SvgPath.Parse("M3 22h7l4-10 6 16 4-10h9"),
        // Two arcs spiralling in to a dot.
        [EffectIcon.Spectrum] = SvgPath.Parse("M18 4a14 14 0 1 1-13 9M18 10a8 8 0 1 1-7 5"),
        // Three arches.
        [EffectIcon.Wave] = SvgPath.Parse("M4 26a14 14 0 0 1 28 0M9 26a9 9 0 0 1 18 0M14 26a4 4 0 0 1 8 0"),
        // A ring struck through.
        [EffectIcon.Off] = SvgPath.Parse("M4 18a14 14 0 1 0 28 0a14 14 0 1 0 -28 0zM8 8l20 20")
    };

    private static readonly Dictionary<EffectIcon, GraphicsPath> Fills = new()
    {
        [EffectIcon.Static] = SvgPath.Parse("M12 18a6 6 0 1 0 12 0a6 6 0 1 0 -12 0z"),
        [EffectIcon.Spectrum] = SvgPath.Parse("M16 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z")
    };

    private readonly (string Name, EffectIcon Icon)[] _effects;
    private readonly int _cellWidth;
    private readonly Spring _bounce = new(1, 0.0005);
    private readonly System.Windows.Forms.Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame;
    private int _selected = -1;
    private int _hovered = -1;

    /// <param name="cellWidth">Each effect's width; the picker is as wide as its effects.</param>
    public EffectPicker(IReadOnlyList<(string Name, EffectIcon Icon)> effects, int cellWidth)
    {
        _effects = [.. effects];
        _cellWidth = cellWidth;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AccessibleRole = AccessibleRole.List;
        BackColor = BackgroundColor;
        Cursor = Cursors.Hand;
        Size = new Size(cellWidth * _effects.Length, PickerHeight);
        TabStop = true;

        _frames.Tick += (_, _) => Frame();
    }

    /// <summary>Raised with the effect clicked, or reached with the keys.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The effect lit, or -1 for none.</summary>
    public int SelectedIndex => _selected;

    /// <summary>The effect picked last, as the user did; -1 before any.</summary>
    public int PickedIndex { get; private set; } = -1;

    /// <summary>Lights an effect (or none with -1) without reporting it: what the laptop shows.</summary>
    public void Select(int index)
    {
        if (index == _selected)
            return;

        var bounce = _selected >= 0 && index >= 0 && IsHandleCreated && Visible && !Motion.Reduced;
        _selected = index;
        AccessibilityNotifyClients(AccessibleEvents.Selection, Math.Max(0, index));

        if (bounce)
        {
            _bounce.Jump(0.94);
            _bounce.To(1, BounceFeel);
            StartFrames();
        }

        Invalidate();
    }

    /// <summary>The name of the effect lit, or empty for none.</summary>
    public string SelectedName => _selected >= 0 ? _effects[_selected].Name : string.Empty;

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var next = e.KeyCode switch
        {
            Keys.Left => Math.Max(0, _selected - 1),
            Keys.Right => Math.Min(_effects.Length - 1, _selected + 1),
            _ => -1
        };

        if (next >= 0 && next != _selected)
        {
            Pick(next);
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hovered = EffectAt(e.X);

        if (hovered != _hovered)
        {
            _hovered = hovered;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (e.Button == MouseButtons.Left && EffectAt(e.X) is var index and >= 0)
            Pick(index);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        for (var index = 0; index < _effects.Length; index++)
        {
            var (name, icon) = _effects[index];
            var cell = new Rectangle(index * _cellWidth, 0, _cellWidth, Height);
            var chosen = index == _selected;
            var color = chosen ? RazerGreen : index == _hovered ? Color.White : RestColor;

            var scale = chosen ? (float)_bounce.Position : 1f;
            var size = IconSize * scale;
            var box = new RectangleF(cell.Left + (cell.Width - size) / 2f, S(4) + (IconSize - size) / 2f, size, size);

            if (chosen)
                PaintIcon(graphics, icon, box, Color.FromArgb(60, RazerGreen), S(4f));

            PaintIcon(graphics, icon, box, color, S(1.6f));

            TextRenderer.DrawText(graphics, name, NameFont, new Rectangle(cell.Left, S(4) + IconSize + S(8), cell.Width, S(20)),
                chosen ? Color.White : NameColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues && chosen)
            {
                using var ring = new Pen(RazerGreen, S(1.5f));
                using var path = RoundedButton.RoundedPath(new RectangleF(cell.Left + S(2), S(1), cell.Width - S(4), Height - S(2)), S(RoundedButton.CornerRadius));
                graphics.DrawPath(ring, path);
            }
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new PickerAccessible(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _frames.Dispose();

        base.Dispose(disposing);
    }

    // The picture on its 36-unit grid, its lines <paramref name="width"/> wide in the window's pixels.
    private static void PaintIcon(Graphics graphics, EffectIcon icon, RectangleF box, Color color, float width)
    {
        var state = graphics.Save();
        graphics.TranslateTransform(box.X, box.Y);
        graphics.ScaleTransform(box.Width / 36f, box.Height / 36f);

        using (var pen = new Pen(color, width * 36f / box.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            graphics.DrawPath(pen, Strokes[icon]);

        if (Fills.TryGetValue(icon, out var fill))
        {
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, fill);
        }

        graphics.Restore(state);
    }

    private void Pick(int index)
    {
        PickedIndex = index;
        Select(index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private int EffectAt(int x)
    {
        var index = x / _cellWidth;
        return x >= 0 && index < _effects.Length ? index : -1;
    }

    private void StartFrames()
    {
        if (!_frames.Enabled)
        {
            _lastFrame = _clock.Elapsed.TotalSeconds;
            _frames.Start();
        }
    }

    private void Frame()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var seconds = Math.Min(0.032, now - _lastFrame);
        _lastFrame = now;

        if (!_bounce.Step(seconds))
            _frames.Stop();

        Invalidate();
    }

    // The list, and an item per effect, for screen readers.
    private sealed class PickerAccessible(EffectPicker owner) : ControlAccessibleObject(owner)
    {
        public override int GetChildCount() => owner._effects.Length;

        public override AccessibleObject? GetChild(int index) =>
            index >= 0 && index < owner._effects.Length ? new EffectAccessible(owner, this, index) : null;
    }

    private sealed class EffectAccessible(EffectPicker owner, AccessibleObject list, int index) : AccessibleObject
    {
        public override string? Name => owner._effects[index].Name;

        public override AccessibleRole Role => AccessibleRole.ListItem;

        public override AccessibleObject? Parent => list;

        public override AccessibleStates State =>
            AccessibleStates.Selectable | (index == owner._selected ? AccessibleStates.Selected : AccessibleStates.None);

        public override string DefaultAction => "Select";

        public override Rectangle Bounds => owner.RectangleToScreen(new Rectangle(index * owner._cellWidth, 0, owner._cellWidth, owner.Height));

        public override void DoDefaultAction() => owner.Pick(index);
    }
}
