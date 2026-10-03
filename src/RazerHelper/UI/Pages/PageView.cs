using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// One section of the main window, shown beside the sidebar: what it holds,
/// top to bottom, inside a 24px margin. Its name is the sidebar's lit entry,
/// so the page does not repeat it. The window tells it when it comes on
/// screen and when it leaves, so a page reads what it shows only meanwhile.
/// </summary>
internal abstract class PageView : UserControl
{
    /// <summary>The width of what a page holds: the page is this plus its 24px margins.</summary>
    public static int ContentWidth => S(552);

    /// <summary>The page's whole width.</summary>
    public static int PageWidth => ContentWidth + 2 * S(24);

    private static int RowPadding => S(12);

    private readonly FlowLayoutPanel _body;

    protected PageView()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _body = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            FlowDirection = FlowDirection.TopDown,
            Location = Point.Empty,
            Margin = Padding.Empty,
            Padding = S(new Padding(24)),
            WrapContents = false
        };

        // As tall as what it holds; the window sets the width (see TrayPopupForm).
        _body.SizeChanged += (_, _) => Height = _body.Height;
        Height = _body.Height;
        Width = PageWidth;

        Controls.Add(_body);
    }

    /// <summary>
    /// Keeps the window open while a question or Windows' permission prompt
    /// has the focus, which would otherwise hide it; dispose to let go. Set by the window.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<IDisposable>? HoldOpen { get; set; }

    /// <summary>The page came on screen: the window is shown with this page in it.</summary>
    public virtual void OnPageShown()
    {
    }

    /// <summary>The page left the screen: another page, or the window hid.</summary>
    public virtual void OnPageHidden()
    {
    }

    /// <summary>Adds a part below the last one.</summary>
    protected void Add(Control part) => _body.Controls.Add(part);

    /// <summary>Holds the window open for as long as the result is not disposed.</summary>
    protected IDisposable KeepOpen() => HoldOpen?.Invoke() ?? NoHold.Instance;

    // A setting's row, 12px above and below: the title and, under it, what it
    // does in quiet grey (left out when empty), on the left, however long the
    // texts run; the control at the right edge, 24px clear of them.
    internal static TableLayoutPanel CreateCard(string text, string hint, Control control)
    {
        control.Margin = Padding.Empty;

        var words = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            WrapContents = false
        };

        words.Controls.Add(new Label
        {
            AutoSize = true,
            Font = SemiBoldFont(16),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            MaximumSize = new Size(ContentWidth - control.Width - S(24), 0),
            Text = L.T(text)
        });

        if (hint.Length > 0)
        {
            words.Controls.Add(new Label
            {
                AutoSize = true,
                Font = DesignFont(14),
                ForeColor = SubtleTextColor,
                Margin = new Padding(0, S(2), 0, 0),
                MaximumSize = new Size(ContentWidth - control.Width - S(24), 0),
                Text = L.T(hint)
            });
        }

        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, RowPadding, 0, RowPadding),
            RowCount = 1
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth - control.Width));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(words, 0, 0);
        row.Controls.Add(control, 1, 0);
        return row;
    }

    /// <summary>
    /// A setting with an on/off switch, as Windows 11 lays it out. Clicking
    /// anywhere on the row flips the switch.
    /// </summary>
    internal static TableLayoutPanel CreateSwitchCard(string text, string hint, out ToggleSwitch toggle)
    {
        var created = toggle = new ToggleSwitch
        {
            AccessibleName = L.T(text),
            Anchor = AnchorStyles.Right
        };

        var card = CreateCard(text, hint, created);

        // The card, the text block and each text in it, but not the switch,
        // which flips itself.
        var words = card.GetControlFromPosition(0, 0)!;

        foreach (var part in words.Controls.Cast<Control>().Append(words).Append(card))
        {
            part.Click += (_, _) => created.Checked = !created.Checked;
            part.Cursor = Cursors.Hand;
        }

        return card;
    }

    /// <summary>The thin line between two settings.</summary>
    internal static Control CreateDivider() => new Panel
    {
        BackColor = DividerColor,
        Height = S(1),
        Margin = Padding.Empty,
        Width = ContentWidth
    };

    /// <summary>The grey line under a card's title.</summary>
    internal static Label HintOf(TableLayoutPanel card) =>
        (Label)card.GetControlFromPosition(0, 0)!.Controls[1];

    private sealed class NoHold : IDisposable
    {
        public static readonly NoHold Instance = new();

        public void Dispose()
        {
        }
    }
}
