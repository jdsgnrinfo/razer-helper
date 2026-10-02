using System.ComponentModel;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public sealed class IdlePlanSwitcherTests
{
    private static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    private readonly FakePlans _plans = new() { ActivePlan = Balanced };
    private readonly FakeClock _clock = new();
    private readonly List<Guid?> _saved = [];

    private IdlePlanSwitcher Create(Guid? before = null) => new(_plans, _clock, before, _saved.Add);

    [Fact]
    public void StartsOff_AndChangesNothing()
    {
        var switcher = Create();
        _clock.Idle = TimeSpan.FromHours(1);

        switcher.Tick();

        Assert.False(switcher.Enabled);
        Assert.Equal(Balanced, _plans.ActivePlan);
    }

    [Fact]
    public void AfterTheMinutes_SwitchesToThePlan_AndSavesThePreviousOne()
    {
        var switcher = Create();
        switcher.Configure(true, 5, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(5);

        switcher.Tick();

        Assert.Equal(PowerSaver, _plans.ActivePlan);
        Assert.True(switcher.Switched);
        Assert.Equal([Balanced], _saved);
    }

    [Fact]
    public void BeforeTheMinutes_LeavesThePlan()
    {
        var switcher = Create();
        switcher.Configure(true, 5, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(4.9);

        switcher.Tick();

        Assert.Equal(Balanced, _plans.ActivePlan);
    }

    [Fact]
    public void BackAtTheKeyboard_PutsThePreviousPlanBack()
    {
        var switcher = Create();
        switcher.Configure(true, 5, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(6);
        switcher.Tick();

        _clock.Idle = TimeSpan.FromSeconds(1);
        switcher.Tick();

        Assert.Equal(Balanced, _plans.ActivePlan);
        Assert.False(switcher.Switched);
        Assert.Equal([Balanced, null], _saved);
    }

    [Fact]
    public void AProgramKeepingTheScreenOn_CountsAsBeingThere()
    {
        var switcher = Create();
        switcher.Configure(true, 1, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(30);
        _clock.ScreenOn = true;

        switcher.Tick();

        Assert.Equal(Balanced, _plans.ActivePlan);
    }

    [Fact]
    public void AVideoStartingWhileSwitched_PutsThePlanBack()
    {
        var switcher = Create();
        switcher.Configure(true, 1, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(2);
        switcher.Tick();

        _clock.ScreenOn = true;
        switcher.Tick();

        Assert.Equal(Balanced, _plans.ActivePlan);
    }

    [Fact]
    public void WithNoPlanChosen_DoesNothing()
    {
        var switcher = Create();
        switcher.Configure(true, 1, null);
        _clock.Idle = TimeSpan.FromMinutes(10);

        switcher.Tick();

        Assert.Equal(Balanced, _plans.ActivePlan);
        Assert.Empty(_saved);
    }

    [Fact]
    public void AlreadyOnThePlan_HasNothingToGiveBack()
    {
        _plans.ActivePlan = PowerSaver;
        var switcher = Create();
        switcher.Configure(true, 1, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(10);

        switcher.Tick();

        Assert.False(switcher.Switched);
        Assert.Empty(_saved);
    }

    [Fact]
    public void TurningItOffWhileSwitched_PutsThePlanBackAtOnce()
    {
        var switcher = Create();
        switcher.Configure(true, 1, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(10);
        switcher.Tick();

        switcher.Configure(false, 1, PowerSaver);

        Assert.Equal(Balanced, _plans.ActivePlan);
        Assert.False(switcher.Switched);
    }

    [Fact]
    public void APlanLeftSwitchedByACrash_ComesBackOnRestore()
    {
        _plans.ActivePlan = PowerSaver;
        var switcher = Create(before: Balanced);

        switcher.Restore();

        Assert.Equal(Balanced, _plans.ActivePlan);
        Assert.Equal([null], _saved);
    }

    [Fact]
    public void APlanThatCannotComeBack_IsForgotten_WithoutThrowing()
    {
        var switcher = Create(before: Balanced);
        _plans.FailActivate = true;

        switcher.Restore();

        Assert.False(switcher.Switched);
    }

    [Fact]
    public void AFailingSwitch_DoesNotThrow()
    {
        var switcher = Create();
        switcher.Configure(true, 1, PowerSaver);
        _clock.Idle = TimeSpan.FromMinutes(10);
        _plans.FailActivate = true;

        switcher.Tick();
        switcher.Tick();
    }

    private sealed class FakePlans : IPowerPlans
    {
        public Guid ActivePlan { get; set; }

        public bool FailActivate { get; set; }

        public IReadOnlyList<PowerPlan> List() => [new(Balanced, "Balanced"), new(PowerSaver, "Power saver")];

        public Guid Active() => ActivePlan;

        public void Activate(Guid plan)
        {
            if (FailActivate)
                throw new Win32Exception(2, "No such plan.");

            ActivePlan = plan;
        }
    }

    private sealed class FakeClock : IIdleClock
    {
        public TimeSpan Idle { get; set; }

        public bool ScreenOn { get; set; }

        public TimeSpan SinceLastInput() => Idle;

        public bool ScreenKeptOn() => ScreenOn;
    }
}
