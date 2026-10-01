using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>One setting of Windows' active power plan, for plugged in and on battery.</summary>
internal interface IPowerPlanValue
{
    /// <summary>The active plan and the setting's values in it.</summary>
    SavedPlanValue Read();

    /// <summary>Writes both values into <paramref name="scheme"/> and applies them if it is the active plan.</summary>
    void Write(Guid scheme, uint pluggedIn, uint onBattery);
}

/// <summary>
/// Sets one power plan setting to <paramref name="silentValue"/> while the
/// laptop is in Silent (the CPU's boost off, or its preference for
/// efficiency), and gives the old value back only when the laptop goes to
/// Balanced or Custom. Closing the app leaves it as it is. What it replaced is
/// kept (see <see cref="SavedPlanValue"/>) and saved with the settings, so it
/// can be put back even after a restart.
/// </summary>
/// <param name="name">What the setting is, for the log.</param>
internal sealed class SilentPlanOverride(
    IPowerPlanValue setting,
    uint silentValue,
    string name,
    SavedPlanValue? saved,
    Action<SavedPlanValue?> persist)
{
    private readonly Lock _sync = new();
    private SavedPlanValue? _saved = saved;
    private PerformanceMode? _mode;

    /// <summary>Whether Silent changes the setting. On by default.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Turns the option on or off. Off gives the old value back at once; on changes it at once if the laptop is in Silent.</summary>
    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            Enabled = enabled;

            if (!enabled)
                RestoreLocked("the option was turned off");
            else if (_mode == PerformanceMode.Silent)
                ApplyLocked();
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
                    ApplyLocked();
                    break;
                case PerformanceMode.Balanced or PerformanceMode.Custom:
                    RestoreLocked($"the laptop went to {mode}");
                    break;
            }
        }
    }

    /// <summary>Gives the old value back now, whatever the mode; for a reset, which clears what was saved.</summary>
    public void Restore()
    {
        lock (_sync)
            RestoreLocked("of a reset");
    }

    private void ApplyLocked()
    {
        // Already changed by us: what we saved is what to give back later.
        if (_saved is not null)
            return;

        try
        {
            var current = setting.Read();

            _saved = current;
            persist(current);
            setting.Write(current.Scheme, silentValue, silentValue);
            AppLog.Info($"Silent: {name} set to {silentValue} (was {current.PluggedIn} plugged in, {current.OnBattery} on battery).");
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not change the {name} for Silent.", exception);
        }
    }

    private void RestoreLocked(string reason)
    {
        if (_saved is not { } saved)
            return;

        try
        {
            setting.Write(saved.Scheme, saved.PluggedIn, saved.OnBattery);
            AppLog.Info($"{name} given back because {reason}.");
        }
        catch (Exception exception)
        {
            // Keep it saved, so the next chance tries again.
            AppLog.Error($"Could not give the {name} back.", exception);
            return;
        }

        _saved = null;
        persist(null);
    }
}
