using RazerHelper.Core.Localization;
using RazerHelper.UI.Pages;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// One line of a list of settings: an outlined icon (when it has one), the
/// setting's name with what it does in quiet grey under it, and its controls
/// at the right edge, all centred on the line; a thin line above it when it
/// follows another. A control that stretches (a slider) takes the room
/// between the words and the rest. It is as wide as a page's wide parts, so
/// a glowing control can reach into the glow room at the right (see
/// <see cref="Add"/>); the words line up with the rest of the page.
/// </summary>
internal sealed class SettingRow : Panel
{
    /// <summary>The line's least height.</summary>
    public static int MinimumHeight => S(56);

    private static int IconSize => S(20);
    private static int Gap => S(14);
    private static readonly Color IconColor = Color.FromArgb(0xBD, 0xBD, 0xBD);
    private static readonly Font TitleFont = DesignFont(15);
    private static readonly Font HintFont = DesignFont(13);

    private readonly NavIcon? _icon;
    private readonly Label _title;

    // The line above, a control of its own so a glowing row of buttons reaching up into it cannot cover it.
    private readonly Panel _line = new() { BackColor = DividerColor, Visible = false };
    private bool _divided;
    private readonly List<(Control Control, int Outset)> _right = [];
    private Control? _fill;
    private int _minimumHeight = MinimumHeight;

    public SettingRow(string title, string hint = "", NavIcon? icon = null)
    {
        _icon = icon;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = BackgroundColor;
        Margin = Padding.Empty;
        Width = PageView.WideWidth;

        _title = new Label
        {
            AutoSize = true,
            BackColor = BackgroundColor,
            Font = TitleFont,
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Text = L.T(title)
        };

        Hint = new Label
        {
            AutoSize = true,
            BackColor = BackgroundColor,
            Font = HintFont,
            ForeColor = SubtleTextColor,
            Margin = Padding.Empty,
            Text = L.T(hint),
            Visible = hint.Length > 0
        };

        Controls.Add(_title);
        Controls.Add(Hint);
        Controls.Add(_line);

        // A changed hint can be longer or go away.
        Hint.TextChanged += (_, _) =>
        {
            Hint.Visible = Hint.Text.Length > 0;
            Arrange();
        };

        Layout += (_, _) => Arrange();
        Arrange();
    }

    /// <summary>The grey line under the name.</summary>
    public Label Hint { get; }

    /// <summary>The name.</summary>
    public Label Title => _title;

    /// <summary>A thin line above, setting the row off from the one before.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Divided
    {
        // Kept in a field: Visible reads false whenever the window is hidden.
        get => _divided;
        set => _line.Visible = _divided = value;
    }

    /// <summary>The words' width when a stretching control follows them; otherwise they take what they need.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int TextWidth { get; set; }

    /// <summary>The line's least height; taller words make it taller.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int RowHeight
    {
        get => _minimumHeight;
        set
        {
            _minimumHeight = value;
            Arrange();
        }
    }

    /// <summary>
    /// Adds a control at the right, left of those added before.
    /// <paramref name="outset"/> is how far it reaches past the content's edge
    /// (a glowing row of buttons, by its glow room), so what shows still ends there.
    /// </summary>
    public SettingRow Add(Control control, int outset = 0)
    {
        control.Margin = Padding.Empty;
        _right.Add((control, outset));
        Controls.Add(control);
        Arrange();
        return this;
    }

    /// <summary>Puts a control that stretches between the words and the controls at the right.</summary>
    public SettingRow Fill(Control control)
    {
        control.Margin = Padding.Empty;
        _fill = control;
        Controls.Add(control);
        Arrange();
        return this;
    }

    /// <summary>Clicking the words, or anywhere on the row outside its controls, flips <paramref name="toggle"/>.</summary>
    public SettingRow FlipsOnClick(ToggleSwitch toggle)
    {
        foreach (var part in new Control[] { this, _title, Hint })
        {
            part.Click += (_, _) => toggle.Checked = !toggle.Checked;
            part.Cursor = Cursors.Hand;
        }

        return this;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);

        var left = UiControls.GlowRoom;

        if (_icon is { } icon)
            NavIcons.Draw(graphics, icon, new RectangleF(left + S(4), (Height - IconSize) / 2f, IconSize, IconSize), IconColor);
    }

    // Places everything for the row's width, and sets its height.
    private void Arrange()
    {
        if (_title is null)
            return;

        var contentLeft = UiControls.GlowRoom + S(4);
        var contentRight = UiControls.GlowRoom + PageView.ContentWidth - S(4);
        var textLeft = contentLeft + (_icon is null ? 0 : IconSize + Gap);

        var titleSize = _title.PreferredSize;
        var hintSize = Hint.Visible ? Hint.PreferredSize : Size.Empty;
        var wordsHeight = titleSize.Height + (Hint.Visible ? hintSize.Height : 0);

        var height = Math.Max(_minimumHeight, wordsHeight + S(16));

        foreach (var (control, outset) in _right)
            height = Math.Max(height, control.Height - 2 * outset + S(16));

        Height = height;

        // The controls at the right, from the edge inwards.
        var right = contentRight;

        foreach (var (control, outset) in _right)
        {
            if (!control.Visible)
                continue;

            control.Location = new Point(right + outset - control.Width, (height - control.Height) / 2);
            right -= control.Width - 2 * outset + Gap;
        }

        var wordsWidth = TextWidth > 0 ? TextWidth : Math.Max(titleSize.Width, hintSize.Width);
        var wordsRoom = Math.Max(0, right + Gap - Gap - textLeft);

        if (_fill is not null)
        {
            var fillLeft = textLeft + wordsWidth + Gap;
            _fill.Bounds = new Rectangle(fillLeft, (height - _fill.Height) / 2, Math.Max(0, right - fillLeft), _fill.Height);
            wordsRoom = wordsWidth;
        }

        _title.MaximumSize = new Size(wordsRoom, 0);
        Hint.MaximumSize = new Size(wordsRoom, 0);

        var top = (height - wordsHeight) / 2;
        _title.Location = new Point(textLeft, top);
        Hint.Location = new Point(textLeft, top + titleSize.Height);

        _line.Bounds = new Rectangle(UiControls.GlowRoom, 0, PageView.ContentWidth, S(1));
        _line.BringToFront();
        Invalidate();
    }
}
