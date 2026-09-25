using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Base for the popup's sections: the shared look (dark, fills its grid cell,
/// a gap below; see GapBelow) and a safe way to update the UI from hardware and power
/// callbacks, which arrive on other threads.
/// </summary>
internal abstract class SectionPanel : Panel
{
    /// <summary>
    /// The space below every section, which sets the sections apart. The
    /// row heights were designed around an 8px gap; the host
    /// adds <see cref="ExtraGap"/> to each section's row for the rest.
    /// </summary>
    public static int GapBelow => S(14);

    /// <summary>How much more room each section row needs than its original 8px gap.</summary>
    public static int ExtraGap => GapBelow - S(8);

    // Post through the UI thread's context instead of Control.BeginInvoke,
    // which needs a window handle. A tray popup has none until it is first
    // shown, and power events can arrive long before that.
    private readonly SynchronizationContext _uiContext;

    protected SectionPanel()
    {
        BackColor = BackgroundColor;
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
