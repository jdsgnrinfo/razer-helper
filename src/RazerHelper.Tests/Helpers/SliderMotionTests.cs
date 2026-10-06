using RazerHelper.UI;

namespace RazerHelper.Tests.Helpers;

public class SliderMotionTests
{
    // Steps a spring at 60 frames a second until it rests, keeping the furthest point it reached.
    private static (double Furthest, int Frames) Settle(Spring spring)
    {
        var furthest = spring.Position;
        var frames = 0;

        while (spring.Step(1 / 60.0) && frames < 600)
        {
            furthest = Math.Max(furthest, spring.Position);
            frames++;
        }

        return (furthest, frames);
    }

    [Fact]
    public void ASpring_SettlesOnItsTarget_InAboutItsTime_WithALittleOvershoot()
    {
        var spring = new Spring(0, 0.01);
        spring.To(100, Spring.Snappy);

        var (furthest, frames) = Settle(spring);

        Assert.Equal(100, spring.Position);
        Assert.False(spring.Moving);
        Assert.InRange(furthest, 100.1, 110);
        Assert.InRange(frames, 10, 60);
    }

    [Fact]
    public void ASpringGivenSpeed_RunsOnPastItsTarget_BeforeComingBack()
    {
        var spring = new Spring(0, 0.01);
        spring.To(0, Spring.Morph, velocity: 150);

        var (furthest, _) = Settle(spring);

        Assert.True(furthest > 3);
        Assert.Equal(0, spring.Position);
    }

    [Fact]
    public void ASpringPutInPlace_IsAtRestThere()
    {
        var spring = new Spring(0, 0.01);
        spring.To(50, Spring.Snappy, velocity: 10);
        spring.Jump(20);

        Assert.Equal(20, spring.Position);
        Assert.Equal(0, spring.Velocity);
        Assert.False(spring.Step(1 / 60.0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(500)]
    [InlineData(-500)]
    public void PastALimit_TheThumbGivesLessThanThePointer_AndNeverMoreThanTheStretch(double pulled)
    {
        var given = SliderPhysics.Rubber(pulled, 9);

        Assert.Equal(Math.Sign(pulled), Math.Sign(given));
        Assert.True(Math.Abs(given) < Math.Abs(pulled));
        Assert.True(Math.Abs(given) < 9);
    }

    [Fact]
    public void AReleaseAfterThePointerStopped_CarriesNoSpeed()
    {
        var trail = new List<(double, double)> { (0, 10), (16, 20), (32, 30) };

        Assert.Equal(625, SliderPhysics.ReleaseVelocity(trail, 40), 3);
        Assert.Equal(0, SliderPhysics.ReleaseVelocity(trail, 200));
        Assert.Equal(0, SliderPhysics.ReleaseVelocity([(0, 10)], 5));
    }

    [Fact]
    public void ASlider_SnapsToItsSteps_AndSaysWhenItsValueChanges()
    {
        using var slider = new ThemedSlider(50, 100, 5) { Width = 300 };
        var changes = 0;
        slider.ValueChanged += (_, _) => changes++;

        slider.Value = 82;
        slider.Value = 81;
        slider.Value = 140;

        Assert.Equal(100, slider.Value);
        Assert.Equal(2, changes); // 80, then 100; 81 is still 80.
    }

    [Fact]
    public void AGlideFromOutside_ChangesTheValueAtOnce()
    {
        using var slider = new ThemedSlider(0, 100, 1) { Width = 300 };

        slider.GlideTo(40);

        Assert.Equal(40, slider.Value);
        Assert.False(slider.IsBeingMoved);
    }

    [Fact]
    public void RollingDigits_ShowTheNewTextAtOnce()
    {
        var text = new RollingText();
        text.Set("95 %", animate: false);
        text.Set("100 %");

        Assert.Equal("100 %", text.Text);
    }
}
