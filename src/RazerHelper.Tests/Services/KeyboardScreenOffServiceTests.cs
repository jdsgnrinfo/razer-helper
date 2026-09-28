using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class KeyboardScreenOffServiceTests
{
    // A keyboard at 80%.
    private sealed class Rig
    {
        public int Brightness = 80;
        public List<int> Writes { get; } = [];
        public KeyboardScreenOffService Service { get; }

        public Rig() =>
            Service = new KeyboardScreenOffService(() => Brightness, value => { Brightness = value; Writes.Add(value); });
    }

    [Fact]
    public void ScreenOff_TurnsTheKeyboardOff_AndScreenOn_BringsTheSameBrightnessBack()
    {
        var rig = new Rig();

        rig.Service.OnDisplayChanged(on: false);
        Assert.Equal(0, rig.Brightness);

        rig.Service.OnDisplayChanged(on: true);
        Assert.Equal(80, rig.Brightness);
    }

    [Fact]
    public void ARepeatedScreenOffKeepsTheOriginalBrightness()
    {
        var rig = new Rig();

        rig.Service.OnDisplayChanged(on: false);
        rig.Service.OnDisplayChanged(on: false);
        rig.Service.OnDisplayChanged(on: true);

        Assert.Equal(80, rig.Brightness);
        Assert.Equal([0, 80], rig.Writes);
    }

    [Fact]
    public void ScreenOnWithoutAnOffChangesNothing()
    {
        var rig = new Rig();

        rig.Service.OnDisplayChanged(on: true);

        Assert.Empty(rig.Writes);
    }

    [Fact]
    public void AKeyboardAlreadyDarkIsNotTouched()
    {
        var rig = new Rig { Brightness = 0 };

        rig.Service.OnDisplayChanged(on: false);
        rig.Service.OnDisplayChanged(on: true);

        Assert.Empty(rig.Writes);
    }

    [Fact]
    public void ABrightnessChangedWhileTheScreenWasOffIsLeftAlone()
    {
        var rig = new Rig();
        rig.Service.OnDisplayChanged(on: false);

        rig.Brightness = 40; // The Fn keys, meanwhile.
        rig.Service.OnDisplayChanged(on: true);

        Assert.Equal(40, rig.Brightness);
    }

    [Fact]
    public void TurnedOff_DoesNothing_AndTurningItOffLightsTheKeyboardAgain()
    {
        var rig = new Rig();
        rig.Service.OnDisplayChanged(on: false);

        rig.Service.SetEnabled(false);
        Assert.Equal(80, rig.Brightness);

        rig.Service.OnDisplayChanged(on: false);
        Assert.Equal(80, rig.Brightness);
    }

    [Fact]
    public void RestoreOnExitLightsTheKeyboardAgain()
    {
        var rig = new Rig();
        rig.Service.OnDisplayChanged(on: false);

        rig.Service.Restore();

        Assert.Equal(80, rig.Brightness);
    }
}
