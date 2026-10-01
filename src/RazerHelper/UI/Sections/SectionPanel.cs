using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Base for the popup's sections: the shared look (flat on the window,
/// filling its grid cell with a gap below; see GapBelow) and a safe way to
/// update the UI from hardware and power callbacks, which arrive on other threads.
/// </summary>
internal abstract class SectionPanel : Panel
{
    /// <summary>
    /// The space below every section, which sets the sections apart: 18px,
    /// the 1px divider line (<see cref="DividerOffset"/>), then 14px to the
    /// next title. The host adds it to each section's row height and draws the line.
    /// </summary>
    public static int GapBelow => S(18 + 1 + 14);

    /// <summary>How far below a section's content its divider line runs.</summary>
    public static int DividerOffset => S(18);

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
        Padding = Padding.Empty;

        // Read here, not in a field initializer: those run before the Control
        // base constructor, which is what may install the context.
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();
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
