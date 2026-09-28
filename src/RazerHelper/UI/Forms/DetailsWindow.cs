using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The shape shared by the details windows (Battery, System): a header with
/// an icon and one bold line of text, the close X, then bento cards. Esc
/// closes; it opens beside the popup; and it reads its figures every couple of
/// seconds, only while it is open.
/// </summary>
internal abstract class DetailsWindow : Form
{
    private const int RefreshIntervalMilliseconds = 2_000;

    protected static int ContentWidth => S(400);

    private readonly IconHeader _header;
    private readonly BentoGrid _cards = new(ContentWidth);
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };
    private Form? _anchor;
    private bool _loggedFailure;

    protected DetailsWindow(string title, Glyph icon)
    {
        _header = new IconHeader(icon);

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = $"RazerHelper {L.T(title)}";

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(20)),
            WrapContents = false
        };

        layout.Controls.Add(CreateTitleRow());
        layout.Controls.Add(_cards);
        Controls.Add(layout);

        _refreshTimer.Tick += (_, _) => Refresh();
    }

    /// <summary>
    /// Reads the figures and returns the header's text and the cards. Called on
    /// open and every couple of seconds; a throw is caught, logged once, and
    /// shown as a card saying the figures could not be read.
    /// </summary>
    protected abstract (string Header, IReadOnlyList<BentoCardSpec> Cards) ReadCards();

    /// <summary>Opens next to the popup instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    /// <summary>Reads and shows the figures now.</summary>
    public new void Refresh()
    {
        string header;
        IReadOnlyList<BentoCardSpec> cards;

        try
        {
            (header, cards) = ReadCards();
        }
        catch (Exception exception)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error($"Could not read the {Text} figures.", exception);
            }

            header = L.T("Unavailable");
            cards = [new BentoCardSpec("Unavailable", L.T("No information"), L.T("Windows did not report these figures"), Wide: true)];
        }

        _header.ShowText(header);
        _cards.ShowCards(cards);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Refresh();
        _refreshTimer.Start();

        // The size is only final once the layout has run.
        PerformLayout();
        PlaceBesideAnchor();
    }

    // Cards can come and go after opening (figures read in the background,
    // a battery that starts reporting), so the window is placed again
    // whenever its size changes, keeping it beside the popup and on screen
    // rather than growing past the bottom of it.
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (IsHandleCreated)
            PlaceBesideAnchor();
    }

    private void PlaceBesideAnchor()
    {
        if (_anchor is null)
            return;

        var workingArea = Screen.FromRectangle(_anchor.Bounds).WorkingArea;
        Location = WindowPlacement.Beside(_anchor.Bounds, Size, workingArea);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
            Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    // The header on the left, the close X on the right.
    private Control CreateTitleRow()
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Height = S(24),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            Width = ContentWidth
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _header.Dock = DockStyle.Fill;
        _header.Margin = Padding.Empty;
        row.Controls.Add(_header, 0, 0);

        var close = new GlyphButton(Glyph.Close, S(18))
        {
            AccessibleName = L.T("Close"),
            Anchor = AnchorStyles.Right,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = S(new Size(24, 24))
        };

        close.Click += (_, _) => Close();
        row.Controls.Add(close, 1, 0);
        return row;
    }

    /// <summary>The header: an icon, then one line of bold text in one color.</summary>
    private sealed class IconHeader : Control
    {
        private static readonly Font HeaderFont = DesignFont(13, FontStyle.Bold);

        private readonly Glyph _icon;
        private string _text = string.Empty;

        public IconHeader(Glyph icon)
        {
            _icon = icon;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void ShowText(string text)
        {
            if (text == _text)
                return;

            _text = text;
            AccessibleName = text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(Parent?.BackColor ?? BackgroundColor);

            var iconSize = S(16);
            Glyphs.Draw(graphics, _icon, new RectangleF(0, (Height - iconSize) / 2f, iconSize, iconSize), Color.White);

            var x = iconSize + S(6);
            TextRenderer.DrawText(graphics, _text, HeaderFont, new Rectangle(x, 0, Width - x, Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }
}
