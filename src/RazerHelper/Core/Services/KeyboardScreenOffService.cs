using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>
/// Turns the keyboard backlight off while the screen is off (Windows' own
/// display timeout, sleep, a closed lid) and brings it back at the brightness
/// it had when the screen comes on. Nothing is polled: it acts only on the
/// display on and off notices Windows sends. "Off" is brightness 0, so the
/// effect and color are kept.
///
/// The light fades rather than snapping, both ways: a few steps over about
/// half a second, on a worker thread, so the window never waits on it. The
/// screen changing its mind mid-fade turns the fade around from wherever it
/// got to. Every write to the keyboard runs in turn on one queue, so two
/// fades never cross.
/// </summary>
internal sealed class KeyboardScreenOffService
{
    // About 0.45 s each way.
    internal const int FadeSteps = 10;
    private static readonly TimeSpan FadeStepDelay = TimeSpan.FromMilliseconds(50);

    private readonly Func<int> _readBrightness;
    private readonly Action<int> _setBrightness;
    private readonly Action<TimeSpan> _pause;
    private readonly Func<Action, Task> _run;
    private readonly Lock _sync = new();

    // The queue of writes: each runs once the one before has finished.
    private Task _queue = Task.CompletedTask;

    // Set from the screen going off until it comes back (or the option is turned off).
    private bool _dimmed;

    // The fade under way or queued, either way; a new one stops it.
    private CancellationTokenSource? _fade;

    // The brightness to bring back, kept until the fade back up has finished;
    // and what the last write left the keyboard at: anything else when the
    // screen comes on means the Fn keys changed it.
    private int? _restoreTo;
    private int _left;

    /// <param name="pause">Waits between fade steps; Thread.Sleep unless a test passes its own.</param>
    /// <param name="run">Starts queued work; a worker thread unless a test runs it in place.</param>
    public KeyboardScreenOffService(Func<int> readBrightness, Action<int> setBrightness, Action<TimeSpan>? pause = null, Func<Action, Task>? run = null)
    {
        _readBrightness = readBrightness;
        _setBrightness = setBrightness;
        _pause = pause ?? Thread.Sleep;
        _run = run ?? (work => Task.Run(work));
    }

    public KeyboardScreenOffService(LightingService lighting)
        : this(lighting.ReadKeyboardBrightness, lighting.SetKeyboardBrightness)
    {
    }

    /// <summary>Off leaves the keyboard alone, and lights it again if it was dimmed.</summary>
    public bool Enabled { get; private set; } = true;

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            Enabled = enabled;

            if (!enabled)
                QueueRestore("the option was turned off");
        }
    }

    /// <summary>Windows reported the screen on (true) or off (false).</summary>
    public void OnDisplayChanged(bool on)
    {
        lock (_sync)
        {
            if (on)
            {
                QueueRestore("the screen came on");
                return;
            }

            if (!Enabled || _dimmed)
                return;

            _dimmed = true;
            var token = StartFade();
            Queue(() => FadeOut(token));
        }
    }

    /// <summary>Lights the keyboard again if this service dimmed it, e.g. as the app exits. Waits until done.</summary>
    public void Restore()
    {
        Task done;

        lock (_sync)
        {
            QueueRestore("RazerHelper is exiting");
            done = _queue;
        }

        done.Wait();
    }

    private void FadeOut(CancellationToken token)
    {
        try
        {
            var from = _readBrightness();

            lock (_sync)
            {
                // A fade back up cut short already holds the brightness to bring back.
                if (_restoreTo is null)
                {
                    // Already dark (dimmed to 0 by hand): nothing to do or undo.
                    if (from <= 0)
                        return;

                    _restoreTo = from;
                }

                _left = from;
            }

            if (Fade(from, 0, token))
                AppLog.Info("Keyboard backlight faded out with the screen.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn the keyboard backlight off with the screen.", exception);
        }
    }

    // Stops a fade under way and queues the fade back up after it.
    private void QueueRestore(string reason)
    {
        _dimmed = false;
        var token = StartFade();
        Queue(() => RestoreNow(reason, token));
    }

    private void RestoreNow(string reason, CancellationToken token)
    {
        int brightness;
        int left;

        lock (_sync)
        {
            if (_restoreTo is not { } saved)
                return;

            brightness = saved;
            left = _left;
        }

        try
        {
            // Changed while the screen was off (the Fn keys): that wins.
            if (_readBrightness() != left)
            {
                lock (_sync)
                    _restoreTo = null;

                return;
            }

            if (!Fade(left, brightness, token))
                return; // Turned around: the fade down that stopped it keeps what to bring back.

            lock (_sync)
                _restoreTo = null;

            AppLog.Info($"Keyboard backlight turned back on ({reason}).");
        }
        catch (Exception exception)
        {
            lock (_sync)
                _restoreTo = null;

            AppLog.Error("Could not turn the keyboard backlight back on.", exception);
        }
    }

    // Steps from one brightness to another, eased: quick at first, softer at
    // the end. False when stopped part way.
    private bool Fade(int from, int to, CancellationToken token)
    {
        for (var step = 1; step <= FadeSteps; step++)
        {
            if (token.IsCancellationRequested)
                return false;

            var remaining = 1 - (double)step / FadeSteps;
            var value = (int)Math.Round(to + (from - to) * remaining * remaining);

            _setBrightness(value);

            lock (_sync)
                _left = value;

            if (step < FadeSteps)
                _pause(FadeStepDelay);
        }

        return true;
    }

    // A fresh token for the next fade, stopping the one before.
    private CancellationToken StartFade()
    {
        _fade?.Cancel();
        _fade = new CancellationTokenSource();
        return _fade.Token;
    }

    private void Queue(Action work)
    {
        var before = _queue;
        _queue = _run(() =>
        {
            before.Wait();
            work();
        });
    }
}
