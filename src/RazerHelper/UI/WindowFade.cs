namespace RazerHelper.UI;

/// <summary>
/// The windows' fade: in as a window is shown and out as it is closed, over
/// <see cref="Milliseconds"/>, eased like the main window's. A window closed
/// by Windows or the app exiting goes at once, and a dialog only fades in:
/// holding back its close would lose its answer.
/// </summary>
internal static class WindowFade
{
    /// <summary>How long a fade takes, in or out.</summary>
    public const double Milliseconds = 100;

    // Per 15 ms tick.
    private const double Step = 15 / Milliseconds;

    public static void Attach(Form form)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        var level = 0.0;
        var target = 1.0;
        var faded = false;

        form.Opacity = 0;

        form.Shown += (_, _) =>
        {
            target = 1;
            timer.Start();
        };

        timer.Tick += (_, _) =>
        {
            level = target > level ? Math.Min(target, level + Step) : Math.Max(target, level - Step);
            form.Opacity = Motion.Ease((float)level);

            if (level != target)
                return;

            timer.Stop();

            if (target == 0)
            {
                faded = true;
                form.Close();
            }
        };

        form.FormClosing += (_, e) =>
        {
            if (faded || e.Cancel || form.Modal || !form.Visible || e.CloseReason != CloseReason.UserClosing)
                return;

            // Closes once faded out (see the tick).
            e.Cancel = true;
            target = 0;
            timer.Start();
        };

        form.FormClosed += (_, _) => timer.Dispose();
    }
}
