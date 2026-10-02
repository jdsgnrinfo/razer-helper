using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>
/// Turns the keyboard backlight off while the screen is off (Windows' own
/// display timeout, sleep, a closed lid) and brings it back at the brightness
/// it had when the screen comes on. Nothing is polled: it acts only on the
/// display on and off notices Windows sends. "Off" is brightness 0, so the
/// effect and color are kept.
///
/// The light fades out rather than snapping off: a few steps down over most
/// of a second, on a worker thread, so the window never waits on it. The
/// screen coming back mid-fade stops the fade and puts the brightness back.
/// Every write to the keyboard runs in turn on one queue, so a fade and the
/// restore after it never cross.
/// </summary>
internal sealed class KeyboardScreenOffService
{
    // About 0.8 s in all.
    internal const int FadeSteps = 16;
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
    private CancellationTokenSource? _fade;

    // The brightness to bring back, and what the fade last left the keyboard
    // at: anything else when the screen comes on means the Fn keys changed it.
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
            _fade = new CancellationTokenSource();
            var token = _fade.Token;
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
            var brightness = _readBrightness();

            // Already dark (dimmed to 0 by hand): nothing to do or undo.
            if (brightness <= 0)
                return;

            lock (_sync)
            {
                _restoreTo = brightness;
                _left = brightness;
            }

            for (var step = 1; step <= FadeSteps; step++)
            {
                if (token.IsCancellationRequested)
                    return;

                // Eased: quick at first, softer as it reaches dark.
                var remaining = 1 - (double)step / FadeSteps;
                var value = (int)Math.Round(brightness * remaining * remaining);

                _setBrightness(value);

                lock (_sync)
                    _left = value;

                if (step < FadeSteps)
                    _pause(FadeStepDelay);
            }

            AppLog.Info("Keyboard backlight faded out with the screen.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn the keyboard backlight off with the screen.", exception);
        }
    }

    // Stops a fade under way and queues the restore after it.
    private void QueueRestore(string reason)
    {
        _fade?.Cancel();
        _fade = null;
        _dimmed = false;
        Queue(() => RestoreNow(reason));
    }

    private void RestoreNow(string reason)
    {
        int brightness;
        int left;

        lock (_sync)
        {
            if (_restoreTo is not { } saved)
                return;

            brightness = saved;
            left = _left;
            _restoreTo = null;
        }

        try
        {
            // Changed while the screen was off (the Fn keys): that wins.
            if (_readBrightness() != left)
                return;

            _setBrightness(brightness);
            AppLog.Info($"Keyboard backlight turned back on ({reason}).");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn the keyboard backlight back on.", exception);
        }
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
