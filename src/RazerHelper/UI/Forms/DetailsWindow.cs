using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The shape shared by the details windows (Battery, System): a header of
/// one bold line of capitals, the close X, then the figures. Esc
/// closes; it opens beside the popup; and it reads its figures every couple of
/// seconds, only while it is open.
/// </summary>
internal abstract class DetailsWindow : Form
{
    private const int RefreshIntervalMilliseconds = 2_000;

    protected static int ContentWidth => S(512);

    private readonly TitleHeader _header = new();
    private readonly BentoGrid _cards;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };
    private Form? _anchor;
    private bool _loggedFailure;

    /// <param name="semiBoldValues">Sets the figures in semi-bold.</param>
    protected DetailsWindow(string title, bool semiBoldValues = false)
    {
        _cards = new BentoGrid(ContentWidth, semiBoldValues);

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
            Padding = S(new Padding(24)),
            WrapContents = false
        };

        layout.Controls.Add(CreateTitleRow());
        layout.Controls.Add(_cards);
        WindowOutline.Attach(layout);
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
        WindowChrome.Apply(Handle, null); // The outline is drawn by WindowOutline instead.
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

    /// <summary>The header: one line of bold white capitals.</summary>
    private sealed class TitleHeader : Control
    {
        private static readonly Font HeaderFont = TitleFont(16, FontStyle.Bold);

        private string _text = string.Empty;

        public TitleHeader()
        {
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

            TextRenderer.DrawText(graphics, _text.ToUpper(System.Globalization.CultureInfo.CurrentUICulture), HeaderFont, new Rectangle(0, 0, Width, Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }
}
