using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>Windows' "processor performance boost mode" in its active power plan, for plugged in and on battery.</summary>
internal interface ICpuBoostSetting
{
    /// <summary>The active plan and its boost modes (0 is off: the CPU stays at its base frequency).</summary>
    SavedCpuBoost Read();

    /// <summary>Writes both boost modes into <paramref name="scheme"/> and applies them if it is the active plan.</summary>
    void Write(Guid scheme, uint pluggedIn, uint onBattery);
}

/// <summary>
/// Keeps the CPU at its base frequency in Silent mode, by turning off Windows'
/// processor boost, and gives it back only when the laptop goes to Balanced or
/// Custom. Closing the app leaves it as it is. What it replaced is kept (see
/// <see cref="SavedCpuBoost"/>) and saved with the settings, so it can be put
/// back even after a restart.
/// </summary>
internal sealed class SilentTurboService(ICpuBoostSetting setting, SavedCpuBoost? saved, Action<SavedCpuBoost?> persist)
{
    private const uint BoostOff = 0;

    private readonly Lock _sync = new();
    private SavedCpuBoost? _saved = saved;
    private PerformanceMode? _mode;

    /// <summary>Whether Silent turns the boost off. On by default.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Turns the option on or off. Off gives the boost back at once; on takes it away at once if the laptop is in Silent.</summary>
    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            Enabled = enabled;

            if (!enabled)
                RestoreLocked("the option was turned off");
            else if (_mode == PerformanceMode.Silent)
                TurnOffLocked();
        }
    }

    /// <summary>The laptop is now in <paramref name="mode"/> (null when unknown).</summary>
    public void OnModeChanged(PerformanceMode? mode)
    {
        lock (_sync)
        {
            _mode = mode;

            switch (mode)
            {
                case PerformanceMode.Silent when Enabled:
                    TurnOffLocked();
                    break;
                case PerformanceMode.Balanced or PerformanceMode.Custom:
                    RestoreLocked($"the laptop went to {mode}");
                    break;
            }
        }
    }

    /// <summary>Gives the boost back now, whatever the mode; for a reset, which clears what was saved.</summary>
    public void Restore()
    {
        lock (_sync)
            RestoreLocked("of a reset");
    }

    private void TurnOffLocked()
    {
        // Already off by us: what we saved is what to give back later.
        if (_saved is not null)
            return;

        try
        {
            var current = setting.Read();

            _saved = current;
            persist(current);
            setting.Write(current.Scheme, BoostOff, BoostOff);
            AppLog.Info($"Silent: CPU boost off (was {current.PluggedIn} plugged in, {current.OnBattery} on battery).");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not turn the CPU boost off for Silent.", exception);
        }
    }

    private void RestoreLocked(string reason)
    {
        if (_saved is not { } saved)
            return;

        try
        {
            setting.Write(saved.Scheme, saved.PluggedIn, saved.OnBattery);
            AppLog.Info($"CPU boost given back because {reason}.");
        }
        catch (Exception exception)
        {
            // Keep it saved, so the next chance tries again.
            AppLog.Error("Could not give the CPU boost back.", exception);
            return;
        }

        _saved = null;
        persist(null);
    }
}
