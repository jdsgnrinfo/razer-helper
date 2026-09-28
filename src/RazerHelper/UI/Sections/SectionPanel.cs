using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Base for the popup's sections: the shared look (a card, as in Settings,
/// filling its grid cell with a gap below; see GapBelow) and a safe way to update the UI from hardware and power
/// callbacks, which arrive on other threads.
/// </summary>
internal abstract class SectionPanel : Panel
{
    /// <summary>
    /// The space below every section, which sets the sections apart. The
    /// row heights were designed around an 8px gap; the host
    /// adds <see cref="ExtraGap"/> to each section's row for the rest.
    /// </summary>
    public static int GapBelow => S(8);

    /// <summary>The space inside the card, around what the section shows.</summary>
    public static Padding CardPadding => S(new Padding(10, 8, 10, 8));

    /// <summary>
    /// How much more room each section row needs than its original 8px gap:
    /// the rest of the gap, and the card's padding above and below.
    /// </summary>
    public static int ExtraGap => GapBelow - S(8) + CardPadding.Vertical;

    // Post through the UI thread's context instead of Control.BeginInvoke,
    // which needs a window handle. A tray popup has none until it is first
    // shown, and power events can arrive long before that.
    private readonly SynchronizationContext _uiContext;

    protected SectionPanel()
    {
        BackColor = CardColor;
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, GapBelow);
        Padding = CardPadding;
        ResizeRedraw = true;

        // Read here, not in a field initializer: those run before the Control
        // base constructor, which is what may install the context.
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();
    }

    // The card: its color, with the buttons' rounded corners, on the window's.
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var fill = new SolidBrush(BackColor);
        using var shape = RoundedButton.RoundedPath(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), S(RoundedButton.CornerRadius));
        graphics.FillPath(fill, shape);
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread, unless the section is already disposed.</summary>
    protected void PostToUi(Action action) =>
        _uiContext.Post(_ =>
        {
            if (!IsDisposed)
                action();
        }, null);

    /// <summary>Like <see cref="PostToUi"/>, but the returned task completes once the action has run, so callers can sequence on it.</summary>
    protected Task PostToUiAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiContext.Post(_ =>
        {
            try
            {
                if (!IsDisposed)
                    action();
            }
            finally
            {
                completion.SetResult();
            }
        }, null);

        return completion.Task;
    }
}
