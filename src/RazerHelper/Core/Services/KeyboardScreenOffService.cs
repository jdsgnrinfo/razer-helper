using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>
/// Turns the keyboard backlight off while the screen is off (Windows' own
/// display timeout, sleep, a closed lid) and brings it back at the brightness
/// it had when the screen comes on. Nothing is polled: it acts only on the
/// display on and off notices Windows sends. "Off" is brightness 0, so the
/// effect and color are kept.
/// </summary>
internal sealed class KeyboardScreenOffService(Func<int> readBrightness, Action<int> setBrightness)
{
    private readonly Lock _sync = new();

    // The brightness to bring back; null while this service has not dimmed the keyboard.
    private int? _restoreTo;

    /// <summary>Off leaves the keyboard alone, and lights it again if it was dimmed.</summary>
    public bool Enabled { get; private set; } = true;

    public KeyboardScreenOffService(LightingService lighting)
        : this(lighting.ReadKeyboardBrightness, lighting.SetKeyboardBrightness)
    {
    }

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            Enabled = enabled;

            if (!enabled)
                RestoreLocked("the option was turned off");
        }
    }

    /// <summary>Windows reported the screen on (true) or off (false).</summary>
    public void OnDisplayChanged(bool on)
    {
        lock (_sync)
        {
            if (on)
            {
                RestoreLocked("the screen came on");
                return;
            }

            if (!Enabled || _restoreTo is not null)
                return;

            try
            {
                var brightness = readBrightness();

                // Already dark (dimmed to 0 by hand): nothing to do or undo.
                if (brightness <= 0)
                    return;

                setBrightness(0);
                _restoreTo = brightness;
                AppLog.Info("Keyboard backlight turned off with the screen.");
            }
            catch (Exception exception)
            {
                AppLog.Error("Could not turn the keyboard backlight off with the screen.", exception);
            }
        }
    }

    /// <summary>Lights the keyboard again if this service dimmed it, e.g. as the app exits.</summary>
    public void Restore()
    {
        lock (_sync)
            RestoreLocked("RazerHelper is exiting");
    }

    private void RestoreLocked(string reason)
    {
        if (_restoreTo is not { } brightness)
            return;

        _restoreTo = null;

        try
        {
            // Changed while the screen was off (the Fn keys): that wins.
            if (readBrightness() != 0)
                return;

            setBrightness(brightness);
            AppLog.Info($"Keyboard backlight turned back on ({reason}).");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn the keyboard backlight back on.", exception);
        }
    }
}
