using System.ComponentModel;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>One of Windows' power plans.</summary>
internal sealed record PowerPlan(Guid Id, string Name);

/// <summary>Windows' power plans: which there are, which is active, and making one active.</summary>
internal interface IPowerPlans
{
    IReadOnlyList<PowerPlan> List();

    Guid Active();

    void Activate(Guid plan);
}

/// <summary>How long the user has been away, as Windows sees it.</summary>
internal interface IIdleClock
{
    /// <summary>The time since the last key press or mouse move.</summary>
    TimeSpan SinceLastInput();

    /// <summary>True while a program asks Windows to keep the screen on, as video players and games do.</summary>
    bool ScreenKeptOn();
}

/// <summary>
/// The Idle option: after the chosen minutes without a key press or mouse
/// move, switches Windows to the chosen power plan, and puts the previous one
/// back exactly as it was as soon as the user is back. A program keeping the
/// screen on (a video, a game played with a controller) counts as being
/// there. The plan it replaced is saved with the settings, so a crash or
/// restart in between still gives it back on the next start.
/// </summary>
internal sealed class IdlePlanSwitcher(IPowerPlans plans, IIdleClock clock, Guid? planBeforeIdle, Action<Guid?> persist)
{
    private Guid? _before = planBeforeIdle;
    private bool _loggedFailure;

    /// <summary>Off until the user turns it on.</summary>
    public bool Enabled { get; private set; }

    public int Minutes { get; private set; } = 5;

    /// <summary>The plan to switch to; nothing happens until there is one.</summary>
    public Guid? Plan { get; private set; }

    /// <summary>True while the idle plan is on and the previous one is waiting to come back.</summary>
    public bool Switched => _before is not null;

    /// <summary>Takes the user's choices. Turning it off, or changing the plan, gives the previous plan back at once.</summary>
    public void Configure(bool enabled, int minutes, Guid? plan)
    {
        var changed = enabled != Enabled || plan != Plan;

        Enabled = enabled;
        Minutes = Math.Max(1, minutes);
        Plan = plan;

        if (changed && Switched)
            Restore();
    }

    /// <summary>Checked every couple of seconds.</summary>
    public void Tick()
    {
        try
        {
            if (Switched)
            {
                // Back at the keyboard (the idle time started over), or a program now keeps the screen on.
                if (!Enabled || !IsAway())
                    Restore();

                return;
            }

            if (!Enabled || Plan is not { } plan || !IsAway())
                return;

            var active = plans.Active();

            if (active == plan)
                return; // Already on it: there is nothing to give back later.

            Remember(active);
            plans.Activate(plan);
            AppLog.Info($"Idle for {Minutes} min: switched the power plan to {plan}.");
        }
        catch (Win32Exception exception)
        {
            // Checked every couple of seconds: log a failure once, not every time.
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error("Could not switch the power plan for idle.", exception);
            }
        }
    }

    /// <summary>Puts the plan from before the idle switch back, if there is one. Also on exit and on a reset.</summary>
    public void Restore()
    {
        if (_before is not { } before)
            return;

        try
        {
            plans.Activate(before);
            AppLog.Info($"Back from idle: power plan {before} restored.");
        }
        catch (Win32Exception exception)
        {
            // The plan may have been deleted meanwhile; there is nothing left to give back.
            AppLog.Error("Could not restore the power plan from before idle.", exception);
        }

        Remember(null);
    }

    private bool IsAway() => clock.SinceLastInput() >= TimeSpan.FromMinutes(Minutes) && !clock.ScreenKeptOn();

    private void Remember(Guid? plan)
    {
        _before = plan;
        persist(plan);
    }
}
