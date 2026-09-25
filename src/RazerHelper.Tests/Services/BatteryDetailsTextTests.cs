using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class BatteryDetailsTextTests
{
    // The figures a Blade 15 Base (2020) reported while charging.
    private static readonly BatteryDetails Charging = new(
        PluggedIn: true, Charging: true, Discharging: false,
        RemainingMilliwattHours: 28906, FullChargeMilliwattHours: 55409, DesignMilliwattHours: 65003,
        RateMilliwatts: 45018, VoltageMillivolts: 16442, EstimatedSecondsLeft: null, CycleCount: null,
        Name: "Blade", Manufacturer: "Razer", Chemistry: "Li-I");

    private static readonly BatteryDetails Discharging = Charging with
    {
        PluggedIn = false, Charging = false, Discharging = true, RateMilliwatts = -18200
    };

    [Fact]
    public void Health_IsTheFullChargeCapacityOverTheDesignCapacity()
    {
        Assert.Equal(85, BatteryDetailsText.HealthPercent(Charging));
        Assert.Equal("85% (55.4 Wh of 65.0 Wh when new)", BatteryDetailsText.Health(Charging));
    }

    [Fact]
    public void Health_IsLeftOutWithoutADesignCapacity()
    {
        Assert.Null(BatteryDetailsText.Health(Charging with { DesignMilliwattHours = null }));
    }

    [Fact]
    public void Charging_ShowsTheRateInAndTheTimeToFull()
    {
        Assert.Equal("Charging", BatteryDetailsText.Status(Charging));
        Assert.Equal("Charging at 45.0 W", BatteryDetailsText.Power(Charging));
        Assert.Equal("About 35 min to full", BatteryDetailsText.Time(Charging)); // 26.5 Wh at 45 W
    }

    [Fact]
    public void OnBattery_ShowsTheDrawAndTheTimeLeft()
    {
        Assert.Equal("On battery", BatteryDetailsText.Status(Discharging));
        Assert.Equal("Using 18.2 W", BatteryDetailsText.Power(Discharging));
        Assert.Equal("About 1 h 35 min left", BatteryDetailsText.Time(Discharging)); // 28.9 Wh at 18.2 W
    }

    [Fact]
    public void OnBattery_PrefersWindowsOwnEstimate()
    {
        Assert.Equal("About 2 h 0 min left", BatteryDetailsText.Time(Discharging with { EstimatedSecondsLeft = 7200 }));
    }

    [Fact]
    public void PluggedInButNotCharging_SaysSoAndShowsNoRateOrTime()
    {
        var full = Charging with { Charging = false, RateMilliwatts = 0 };

        Assert.Equal("Plugged in, not charging", BatteryDetailsText.Status(full));
        Assert.Null(BatteryDetailsText.Power(full));
        Assert.Null(BatteryDetailsText.Time(full));
    }

    [Fact]
    public void Charge_ShowsWattHoursAndPercent()
    {
        Assert.Equal("28.9 Wh of 55.4 Wh (52%)", BatteryDetailsText.Charge(Charging));
    }

    [Fact]
    public void Identity_NamesTheBatteryAndSpellsOutAShortChemistryCode()
    {
        Assert.Equal("Razer Blade · Lithium-ion", BatteryDetailsText.Identity(Charging));
    }

    [Fact]
    public void Rows_LeaveOutWhatTheBatteryDoesNotReport()
    {
        var labels = BatteryDetailsText.Rows(Charging).Select(row => row.Label).ToArray();

        Assert.Equal(["Status", "Power", "Time", "Charge", "Health", "Voltage", "Battery"], labels);
    }
}
