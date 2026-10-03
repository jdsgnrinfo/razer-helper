using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The figures of a page (Battery, System) as cards, two to a row. It reads
/// them every couple of seconds, only while its page is on screen (see
/// <see cref="Start"/>).
/// </summary>
internal abstract class DetailsView : FlowLayoutPanel
{
    private const int RefreshIntervalMilliseconds = 2_000;

    private readonly BentoGrid _cards;

    // In the cards' place while they are still being read.
    private readonly LoadingSpinner _spinner = new()
    {
        Margin = Padding.Empty,
        Size = new Size(PageView.ContentWidth, S(140)),
        Visible = false
    };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = RefreshIntervalMilliseconds };
    private bool _loggedFailure;

    /// <param name="name">What the figures are, in English, for the log.</param>
    /// <param name="semiBoldValues">Sets the figures in semi-bold.</param>
    protected DetailsView(string name, bool semiBoldValues = false)
    {
        Name = name;
        _cards = new BentoGrid(PageView.ContentWidth, semiBoldValues);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        FlowDirection = FlowDirection.TopDown;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        WrapContents = false;

        Controls.Add(_cards);
        Controls.Add(_spinner);

        _refreshTimer.Tick += (_, _) => Refresh();
    }

    /// <summary>
    /// Reads the figures and returns the cards. Called as the
    /// page comes on screen and every couple of seconds; a throw is caught,
    /// logged once, and shown as a card saying the figures could not be read.
    /// </summary>
    protected abstract IReadOnlyList<BentoCardSpec> ReadCards();

    /// <summary>True while the figures are still being read: a spinner shows instead of the cards.</summary>
    protected virtual bool IsLoading => false;

    /// <summary>Reads the figures now, then every couple of seconds.</summary>
    public virtual void Start()
    {
        Refresh();
        _refreshTimer.Start();
    }

    public void Stop() => _refreshTimer.Stop();

    /// <summary>Reads and shows the figures now.</summary>
    public new void Refresh()
    {
        IReadOnlyList<BentoCardSpec> cards;

        try
        {
            cards = ReadCards();
        }
        catch (Exception exception)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error($"Could not read the {Name} figures.", exception);
            }

            cards = [new BentoCardSpec("Unavailable", L.T("No information"), L.T("Windows did not report these figures"), Wide: true)];
        }

        _cards.ShowCards(cards);

        var loading = IsLoading;
        _spinner.Visible = loading;
        _cards.Visible = !loading;
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
}
